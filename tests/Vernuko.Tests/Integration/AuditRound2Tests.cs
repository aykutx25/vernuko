using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Commerce;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.ServiceDesk;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Regression tests for the findings of the second review (0.16.1) (each was reproduced before the fix).</summary>
public class AuditRound2Tests
{
    private static async Task<(TestApp App, CommerceService Commerce, ProductDto Drill)> ShopAsync()
    {
        var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        var inv = app.Get<InventoryService>();
        var id = (await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Matkap ucu seti", "M-13", "adet", 500, 0, 0, true, 0, "8690000000017"))).Value;
        await inv.ReceiveAsync(id, 10, 200, "İlk stok");
        return (app, app.Get<CommerceService>(), (await inv.GetProductsAsync()).Value.Single(p => p.Id == id));
    }

    [Fact]
    public async Task The_same_sale_line_twice_in_one_return_cannot_take_back_more_than_was_sold()
    {
        var (app, commerce, drill) = await ShopAsync();
        await using var _ = app;
        var sale = (await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(drill.Id, 2, 500)], 0, 1_000, 0, 0, null, "s-1"))).Value;
        var line = sale.Lines.Single().Id;

        var r = await commerce.CreateReturnAsync(new CreateReturnRequest(sale.Id, [new(line, 2), new(line, 2)], 2_000, 0, 0, null, "r-1"));
        Assert.Equal("validation.return.quantity", r.Error.Code);
        Assert.Equal(8, (await app.Get<InventoryService>().GetProductsAsync()).Value.Single().Stock);
    }

    [Fact]
    public async Task Partial_returns_together_never_pay_back_more_than_the_line_brought_in()
    {
        var (app, commerce, drill) = await ShopAsync();
        await using var _ = app;

        // 2 × 5,00 − 0,01 line discount = 9,99 paid.
        var sale = (await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(drill.Id, 2, 500, 1)], 0, 999, 0, 0, null, "s-1"))).Value;
        var line = sale.Lines.Single().Id;
        var first = (await commerce.CreateReturnAsync(new CreateReturnRequest(sale.Id, [new(line, 1)], 500, 0, 0, null, "r-1"))).Value;
        var second = await commerce.CreateReturnAsync(new CreateReturnRequest(sale.Id, [new(line, 1)], 499, 0, 0, null, "r-2"));
        Assert.True(second.IsSuccess, second.IsFailure ? second.Error.ToString() : null);
        Assert.Equal(999, first.TotalMinor + second.Value.TotalMinor);
        Assert.Equal(0, (await commerce.GetDayEndAsync(new DateOnly(2026, 11, 12))).Value.NetSalesMinor);
    }

    [Fact]
    public async Task The_walk_in_account_is_not_a_customer_and_cannot_buy_on_credit()
    {
        var (app, commerce, drill) = await ShopAsync();
        await using var _ = app;
        Assert.True((await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(drill.Id, 1, 500)], 0, 500, 0, 0, null, "s-1"))).IsSuccess);
        Assert.Empty((await app.Get<CustomerService>().SearchAsync("Perakende")).Value);

        await using var db = await app.DbAsync();
        var walkIn = await db.Customers.Where(c => c.FirstName == "Perakende" && c.LastName == "Satış").Select(c => c.Id).SingleAsync();
        var unpaid = await commerce.CreateSaleAsync(new CreateSaleRequest(walkIn, [new(drill.Id, 1, 500)], 0, 0, 0, 0, null, "s-2"));
        Assert.Equal("validation.sale.on_account", unpaid.Error.Code);
    }

    [Fact]
    public void A_document_discount_is_shared_exactly_and_never_reaches_a_free_or_too_small_line()
    {
        LineInput Line(long price) => new(Guid.NewGuid(), "Ürün", 1, price, 20);

        var withGift = Sale.Create("S-2026-000001", Guid.NewGuid(), [Line(1_000), Line(1_000), Line(0)], 1, 1_999, 0, 0, false, null, null, TestApp.DefaultNow.UtcDateTime).Value;
        Assert.Equal(0, withGift.Lines[2].NetTotalMinor);
        Assert.Equal(1_999, withGift.Lines.Sum(l => l.NetTotalMinor));

        var pennies = Sale.Create("S-2026-000002", Guid.NewGuid(), [Line(1), Line(1), Line(1), Line(1), Line(1)], 2, 3, 0, 0, false, null, null, TestApp.DefaultNow.UtcDateTime).Value;
        Assert.All(pennies.Lines, l => Assert.InRange(l.NetTotalMinor, 0, 1));
        Assert.Equal(3, pennies.Lines.Sum(l => l.NetTotalMinor));
    }

    [Fact]
    public async Task New_ledgers_are_append_only_in_the_database()
    {
        var (app, commerce, drill) = await ShopAsync();
        await using var _ = app;
        var supplier = (await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Ece Yapı", null, null, null, null, null, null, null, true, 0))).Value;
        await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(supplier, "F-1", new DateOnly(2026, 11, 12), [new(drill.Id, 1, 200, 20)], false, 0, null, null, "p-1"));
        await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new(drill.Id, 1, 500)], 0, 500, 0, 0, null, "s-1"));

        await using var db = await app.DbAsync();
        foreach (var sql in new[]
        {
            "UPDATE SupplierTransactions SET AmountMinor = 1", "DELETE FROM SupplierTransactions",
            "UPDATE SaleLines SET Quantity = 99", "DELETE FROM PurchaseLines", "UPDATE Purchases SET DocumentNo = 'x'", "DELETE FROM Sales",
        })
        {
            var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Contains(DatabaseTriggers.AppendOnlyError, ex.Message, StringComparison.Ordinal);
        }
    }

    private static TicketIntakeRequest Phone(Guid customer, string serial) =>
        new(customer, DeviceKind.Phone, "Telsa", "T12", serial, "Ekran kırık", null, null, null, false, null, null, null);

    private static async Task<(TestApp App, ServiceDeskService Desk, Guid Customer, Guid Part)> ServiceAsync()
    {
        var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.PhoneRepair);
        var customer = await app.AddCustomerAsync();
        var part = (await app.Get<InventoryService>().SaveProductAsync(new ProductDto(Guid.Empty, "Ekran", "EK", "adet", 10_000, 5_000, 0, true, 0))).Value;
        await app.Get<InventoryService>().ReceiveAsync(part, 5, 5_000, "İlk stok");
        return (app, app.Get<ServiceDeskService>(), customer, part);
    }

    private static async Task<TicketDto> DeliveredRepairAsync(ServiceDeskService desk, Guid customer, string serial, string key)
    {
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, serial), null, key))).Value;
        await desk.AddLaborAsync(t.Summary.Id, "İşçilik", 1_000, null);
        await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, false);
        return (await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 1_000, 0, 0, key + "-d"))).Value;
    }

    [Fact]
    public async Task Warranty_applies_only_to_the_same_device_and_only_within_a_year()
    {
        var (app, desk, customer, _) = await ServiceAsync();
        await using var _ = app;
        var earlier = await DeliveredRepairAsync(desk, customer, "352099001761481", "t-1");

        var other = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "999999999999999"), null, "t-2"))).Value;
        Assert.Equal("validation.ticket.warranty_device", (await desk.LinkWarrantyAsync(other.Summary.Id, earlier.Summary.Id)).Error.Code);

        app.Time.Advance(TimeSpan.FromDays(400));
        var late = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "352099001761481"), null, "t-3"))).Value;
        Assert.Equal("validation.ticket.warranty_expired", (await desk.LinkWarrantyAsync(late.Summary.Id, earlier.Summary.Id)).Error.Code);
    }

    [Fact]
    public async Task Delivery_retry_kapora_after_delivery_and_customer_change_after_kapora()
    {
        var (app, desk, customer, _) = await ServiceAsync();
        await using var _ = app;

        // A free delivery sent twice (no ledger entries to recognise it by) answers with the delivered ticket.
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "111111111111111"), null, "t-1"))).Value;
        await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, false);
        Assert.True((await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 0, 0, 0, "d-1"))).IsSuccess);
        var again = await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 0, 0, 0, "d-2"));
        Assert.True(again.IsSuccess, again.IsFailure ? again.Error.ToString() : null);
        Assert.Equal(TicketStatus.Delivered, again.Value.Summary.Status);

        Assert.Equal("validation.ticket.closed", (await desk.TakeDepositAsync(t.Summary.Id, 1_000, PaymentMethod.Cash, "k-1")).Error.Code);

        var open = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "222222222222222"), null, "t-2"))).Value;
        Assert.True((await desk.TakeDepositAsync(open.Summary.Id, 1_000, PaymentMethod.Cash, "k-2")).IsSuccess);
        var someoneElse = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        Assert.Equal("validation.ticket.customer_deposit", (await desk.UpdateIntakeAsync(open.Summary.Id, Phone(someoneElse, "222222222222222"))).Error.Code);
    }

    [Fact]
    public async Task A_part_or_labour_line_sent_twice_with_the_same_key_is_added_once()
    {
        var (app, desk, customer, part) = await ServiceAsync();
        await using var _ = app;
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "333333333333333"), null, "t-1"))).Value;

        await desk.AddPartAsync(t.Summary.Id, part, 1, null, "part-1");
        await desk.AddPartAsync(t.Summary.Id, part, 1, null, "part-1");
        await desk.AddLaborAsync(t.Summary.Id, "İşçilik", 1_000, null, "labor-1");
        var final = (await desk.AddLaborAsync(t.Summary.Id, "İşçilik", 1_000, null, "labor-1")).Value;

        Assert.Equal(2, final.Lines.Count);
        Assert.Equal(4, (await app.Get<InventoryService>().GetProductsAsync()).Value.Single(p => p.Id == part).Stock);
    }

    [Fact]
    public async Task History_and_audit_hold_no_names_of_who_brought_approved_or_collected()
    {
        var (app, desk, customer, _) = await ServiceAsync();
        await using var _ = app;
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "444444444444444") with { BroughtBy = "Mehmet Getiren" }, null, "t-1"))).Value;
        await desk.SetEstimateAsync(t.Summary.Id, 1_000, 0);
        await desk.RecordApprovalAsync(t.Summary.Id, true, ApprovalChannel.Phone, "Onaylayan Kişi");
        await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, false);
        var done = (await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Teslim Alan", 1_000, 0, 0, "d-1"))).Value;

        Assert.DoesNotContain(done.History, e => e.Text.Contains("Mehmet", StringComparison.Ordinal) || e.Text.Contains("Onaylayan", StringComparison.Ordinal) || e.Text.Contains("Teslim Alan", StringComparison.Ordinal));
        await using var db = await app.DbAsync();
        Assert.False(await db.AuditEntries.AnyAsync(a => a.Summary.Contains("Teslim Alan")));
    }
}
