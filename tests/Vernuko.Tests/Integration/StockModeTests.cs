using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Commerce;
using Vernuko.Inventory;
using Vernuko.Licensing;
using Vernuko.Payments;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class StockModeTests
{
    private static async Task<(ProductDto Drill, ProductDto Screws)> ProductsAsync(TestApp app, Guid? supplierId = null)
    {
        var inv = app.Get<InventoryService>();
        var drill = await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Matkap ucu seti", "M-13", "adet", 45_000, 0, 5, true, 0, "8690000000017", 20, "El aletleri", "A-03", supplierId));
        var screws = await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Vida 4x40 100'lü", null, "kutu", 12_000, 0, 10, true, 0, "8690000000024", 20, "Bağlantı", "B-07", supplierId));
        Assert.True(drill.IsSuccess && screws.IsSuccess, drill.IsFailure ? drill.Error.ToString() : screws.IsFailure ? screws.Error.ToString() : null);
        var all = (await inv.GetProductsAsync()).Value;
        return (all.Single(p => p.Id == drill.Value), all.Single(p => p.Id == screws.Value));
    }

    private static async Task<Guid> SupplierAsync(TestApp app) =>
        (await app.Get<CommerceService>().SaveSupplierAsync(new SupplierDto(Guid.Empty, "Ece Yapı Ltd.", "Nilüfer", "0000000001", "0224 111 22 33", null, null, "TR33 0006 1005 1978 6457 8413 26", null, true, 0))).Value;

    [Fact]
    public async Task Stock_and_service_modes_need_their_own_license()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["k1"] = key.ExportSubjectPublicKeyInfo() } };
        await using var app = await TestApp.CreateAsync(licensing: options);
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        Assert.Equal(BusinessMode.Stok, (await app.Get<SettingsService>().GetBusinessInfoAsync())!.Mode);

        LicenseDocument Doc(string product) => new()
        {
            LicenseId = "lic_1", Product = product, Edition = "Standart", ActivationId = "act_1", DeviceHash = app.Device.DeviceHash,
            IssuedAtUtc = TestApp.DefaultNow.UtcDateTime, ValidUntilUtc = TestApp.DefaultNow.UtcDateTime.AddDays(365), RefreshAfterUtc = TestApp.DefaultNow.UtcDateTime.AddDays(7),
        };

        var randevu = await app.Get<LicenseService>().ImportAsync(LicenseVerifier.Sign(Doc(LicenseProducts.Randevu), key, "k1"));
        Assert.Equal("license.wrong_product", randevu.Error.Code);
        Assert.Contains("Vernuko Stok", randevu.Error.Message, StringComparison.Ordinal);
        Assert.True((await app.Get<LicenseService>().ImportAsync(LicenseVerifier.Sign(Doc(LicenseProducts.Stok), key, "k1"))).IsSuccess);
    }

    [Fact]
    public async Task A_walk_in_sale_by_barcode_takes_stock_out_and_books_the_payments()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        var commerce = app.Get<CommerceService>();
        var supplier = await SupplierAsync(app);
        var (drill, screws) = await ProductsAsync(app, supplier);

        var receipt = await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(supplier, "IRS-4471", new DateOnly(2026, 11, 12),
            [new PurchaseLineRequest(drill.Id, 10, 25_000, 20), new PurchaseLineRequest(screws.Id, 40, 6_000, 20, 4_000)], true, 100_000, PaymentMethod.BankTransfer, null, "p-1"));
        Assert.True(receipt.IsSuccess, receipt.IsFailure ? receipt.Error.ToString() : null);
        Assert.Equal("A-2026-000001", receipt.Value.Summary.Number);
        Assert.Equal(250_000 + 236_000, receipt.Value.Summary.NetMinor);
        Assert.Equal(97_200, receipt.Value.Summary.VatMinor);

        var found = (await commerce.FindProductAsync(" 8690000000017 ")).Value!;
        Assert.Equal(drill.Id, found.Id);
        Assert.Equal(10, found.Stock);
        Assert.Equal(25_000, found.CostMinor); // last purchase cost
        Assert.Equal(5_900, (await commerce.FindProductAsync("8690000000024")).Value!.CostMinor); // after the line discount
        Assert.Equal(drill.Id, (await commerce.FindProductAsync("m-13")).Value!.Id); // product code, any case

        var request = new CreateSaleRequest(null, [new SaleLineRequest(drill.Id, 2, 45_000), new SaleLineRequest(screws.Id, 3, 12_000, 1_000)], 0, 100_000, 25_000, 0, null, "s-1");
        var sale = await commerce.CreateSaleAsync(request);
        Assert.True(sale.IsSuccess, sale.IsFailure ? sale.Error.ToString() : null);
        Assert.Equal("S-2026-000001", sale.Value.Number);
        Assert.True(sale.Value.IsWalkIn);
        Assert.Equal(125_000, sale.Value.TotalMinor); // 90.000 + 36.000 − 1.000 line discount
        Assert.Equal(20_833, sale.Value.VatMinor);
        Assert.Equal(0, sale.Value.OnAccountMinor);

        // A double click or a retry gives the same sale and takes no more stock.
        Assert.Equal(sale.Value.Id, (await commerce.CreateSaleAsync(request)).Value.Id);
        Assert.Equal(8, (await commerce.FindProductAsync("8690000000017")).Value!.Stock);

        var day = (await commerce.GetDayEndAsync(new DateOnly(2026, 11, 12))).Value;
        Assert.Equal(1, day.SaleCount);
        Assert.Equal(125_000, day.NetSalesMinor);
        Assert.Equal(100_000, day.CashMinor);
        Assert.Equal(25_000, day.CardMinor);
        Assert.Equal(20_833, Assert.Single(day.Vat).VatMinor);
        Assert.Equal(2 * 25_000 + 3 * 5_900, day.CostMinor);

        // Walk-in: everything must be paid.
        var short_ = await commerce.CreateSaleAsync(new CreateSaleRequest(null, [new SaleLineRequest(drill.Id, 1, 45_000)], 0, 40_000, 0, 0, null, "s-2"));
        Assert.Equal("validation.sale.on_account", short_.Error.Code);

        // Supplier: owed the invoice total minus what was paid at the door.
        var statement = (await commerce.GetSupplierStatementAsync(supplier)).Value;
        Assert.Equal(583_200 - 100_000, statement.Supplier.BalanceMinor);
        Assert.Equal("A-2026-000001", statement.Entries[0].DocumentNumber);
        Assert.True((await commerce.PaySupplierAsync(new SupplierPaymentRequest(supplier, 483_200, PaymentMethod.Cash, null, "pay-1"))).IsSuccess);
        Assert.True((await commerce.PaySupplierAsync(new SupplierPaymentRequest(supplier, 483_200, PaymentMethod.Cash, null, "pay-1"))).IsSuccess);
        Assert.Equal(0, (await commerce.GetSuppliersAsync()).Value.Single().BalanceMinor);
    }

    [Fact]
    public async Task Credit_sales_stay_on_the_customer_account_and_returns_take_back_only_what_was_sold()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        var commerce = app.Get<CommerceService>();
        var (drill, _) = await ProductsAsync(app);
        var customer = await app.AddCustomerAsync("Usta", "Elektrik");

        var sale = (await commerce.CreateSaleAsync(new CreateSaleRequest(customer, [new SaleLineRequest(drill.Id, 4, 45_000)], 0, 30_000, 0, 0, "30 gün vadeli", "v-1"))).Value;
        Assert.Equal(150_000, sale.OnAccountMinor);
        Assert.Equal(150_000, (await app.Get<CustomerService>().GetAsync(customer)).Value.BalanceMinor);
        Assert.Equal(-4, (await commerce.FindProductAsync("8690000000017")).Value!.Stock); // selling below zero is allowed; the screen warns

        // Two of four back: the customer is credited 90.000 (nothing paid back in cash).
        var line = sale.Lines.Single();
        var ret = await commerce.CreateReturnAsync(new CreateReturnRequest(sale.Id, [new ReturnLineRequest(line.Id, 2)], 0, 0, 0, "Fazla alınmış", "r-1"));
        Assert.True(ret.IsSuccess, ret.IsFailure ? ret.Error.ToString() : null);
        Assert.Equal("SI-2026-000001", ret.Value.Number);
        Assert.Equal(SaleKind.Return, ret.Value.Kind);
        Assert.Equal(sale.Number, ret.Value.ReturnOfNumber);
        Assert.Equal(60_000, (await app.Get<CustomerService>().GetAsync(customer)).Value.BalanceMinor);
        Assert.Equal(-2, (await commerce.FindProductAsync("8690000000017")).Value!.Stock);
        Assert.Equal(2, (await commerce.GetSaleAsync(sale.Id)).Value.Lines.Single().ReturnableQuantity);

        var tooMany = await commerce.CreateReturnAsync(new CreateReturnRequest(sale.Id, [new ReturnLineRequest(line.Id, 3)], 0, 0, 0, null, "r-2"));
        Assert.Equal("validation.return.quantity", tooMany.Error.Code);
        Assert.Equal("validation.sale.return_of_return", (await commerce.CreateReturnAsync(new CreateReturnRequest(ret.Value.Id, [new ReturnLineRequest(ret.Value.Lines[0].Id, 1)], 0, 0, 0, null, "r-3"))).Error.Code);

        var day = (await commerce.GetDayEndAsync(new DateOnly(2026, 11, 12))).Value;
        Assert.Equal(1, day.ReturnCount);
        Assert.Equal(180_000 - 90_000, day.NetSalesMinor);
        Assert.Equal(30_000, day.CashMinor);
        Assert.Equal(150_000 - 90_000, day.OnAccountMinor);
    }

    [Fact]
    public async Task A_stock_count_writes_only_the_differences_once_and_low_stock_is_suggested_by_supplier()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        var commerce = app.Get<CommerceService>();
        var supplier = await SupplierAsync(app);
        var (drill, screws) = await ProductsAsync(app, supplier);
        await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(supplier, null, new DateOnly(2026, 11, 12), [new PurchaseLineRequest(drill.Id, 10, 25_000, 20), new PurchaseLineRequest(screws.Id, 30, 6_000, 20)], true, 0, null, null, "p-1"));

        app.Time.Advance(TimeSpan.FromMinutes(5));
        var count = new StockCountRequest([new StockCountLine(drill.Id, 4), new StockCountLine(screws.Id, 30)], "Yıl sonu", "c-1");
        var result = await commerce.ApplyStockCountAsync(count);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.Equal(1, result.Value.AdjustedProducts);
        Assert.Equal(-6, result.Value.QuantityDifference);
        Assert.Equal(-150_000, result.Value.ValueDifferenceMinor);
        Assert.Equal("conflict.count.done", (await commerce.ApplyStockCountAsync(count)).Error.Code);
        Assert.Equal(4, (await commerce.FindProductAsync("8690000000017")).Value!.Stock);
        var movement = (await app.Get<InventoryService>().GetMovementsAsync(drill.Id)).Value.First();
        Assert.Equal(StockMovementKind.Adjusted, movement.Kind);
        Assert.Contains("kayıtta 10, sayılan 4", movement.Note, StringComparison.Ordinal);

        var reorder = (await commerce.GetReorderSuggestionsAsync()).Value;
        var group = Assert.Single(reorder);
        Assert.Equal("Ece Yapı Ltd.", group.SupplierName);
        var suggestion = Assert.Single(group.Lines);
        Assert.Equal(drill.Id, suggestion.ProductId);
        Assert.Equal(6, suggestion.SuggestedQuantity); // refill to twice the critical level (5 × 2 − 4)

        var value = (await commerce.GetStockValueAsync()).Value;
        Assert.Equal(4 * 25_000 + 30 * 6_000, value.CostValueMinor);
    }

    [Fact]
    public async Task Barcodes_are_unique_and_suppliers_are_checked()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.Stationery);
        var inv = app.Get<InventoryService>();
        await ProductsAsync(app);
        var dup = await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Başka ürün", null, "adet", 1_000, 0, 0, true, 0, "8690000000017"));
        Assert.Equal("conflict.product.barcode_taken", dup.Error.Code);
        Assert.Equal("validation.product.vat", (await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Kalem", null, "adet", 1_000, 0, 0, true, 0, null, 18))).Error.Code);

        var commerce = app.Get<CommerceService>();
        Assert.Equal("validation.supplier.tax_number", (await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Hatalı", null, "1234567891", null, null, null, null, null, true, 0))).Error.Code);
        Assert.Equal("validation.supplier.iban", (await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Hatalı", null, null, null, null, null, "TR00 0000 0000 0000 0000 0000 00", null, true, 0))).Error.Code);
        Assert.True(Supplier.IsValidIban("TR330006100519786457841326"));
    }

    [Fact]
    public void Sale_math_spreads_the_document_discount_and_counts_kdv_inside_prices()
    {
        var lines = new[] { new LineInput(Guid.NewGuid(), "A", 1, 12_000, 20), new LineInput(Guid.NewGuid(), "B", 1, 11_000, 10) };
        var sale = Sale.Create("S-2026-000001", Guid.NewGuid(), lines, 2_300, 20_700, 0, 0, false, null, null, TestApp.DefaultNow.UtcDateTime).Value;
        Assert.Equal(20_700, sale.TotalMinor);
        Assert.Equal(20_700, sale.Lines.Sum(l => l.NetTotalMinor));
        Assert.Equal(10_800, sale.Lines[0].NetTotalMinor);
        Assert.Equal(Vat.IncludedIn(10_800, 20) + Vat.IncludedIn(9_900, 10), sale.VatMinor);
        Assert.Equal(1_800, Vat.IncludedIn(10_800, 20));
        Assert.Equal("validation.sale.overpaid", Sale.Create("S-1", Guid.NewGuid(), lines, 0, 30_000, 0, 0, true, null, null, TestApp.DefaultNow.UtcDateTime).Error.Code);
        Assert.Equal("validation.sale.vat", Sale.Create("S-1", Guid.NewGuid(), [new LineInput(Guid.NewGuid(), "A", 1, 100, 18)], 0, 100, 0, 0, false, null, null, TestApp.DefaultNow.UtcDateTime).Error.Code);
    }
}
