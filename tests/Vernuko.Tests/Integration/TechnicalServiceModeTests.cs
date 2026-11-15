using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Inventory;
using Vernuko.Messaging;
using Vernuko.Payments;
using Vernuko.ServiceDesk;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class TechnicalServiceModeTests
{
    private static TicketIntakeRequest Phone(Guid customer, string serial = "35-209900-176148-1") =>
        new(customer, DeviceKind.Phone, "Telsa", "T12", serial, "Ekran kırık, dokunmatik çalışmıyor", "Kılıf, şarj aleti", "Arka kapakta çizik", "Ayşe Kara", false, null, null, null);

    private static async Task<(TestApp App, ServiceDeskService Desk, Guid Customer, Guid Screen)> StartAsync()
    {
        var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.PhoneRepair);
        var customer = await app.AddCustomerAsync();
        var screen = (await app.Get<InventoryService>().SaveProductAsync(new ProductDto(Guid.Empty, "T12 ekran", "EKR-T12", "adet", 250_000, 120_000, 1, true, 0))).Value;
        await app.Get<InventoryService>().ReceiveAsync(screen, 3, 120_000, "İlk stok");
        return (app, app.Get<ServiceDeskService>(), customer, screen);
    }

    [Fact]
    public async Task A_phone_goes_from_intake_through_price_approval_and_parts_to_delivery()
    {
        var (app, desk, customer, screen) = await StartAsync();
        await using var _ = app;

        var opened = await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer), "1-4-7-8", "t-1"));
        Assert.True(opened.IsSuccess, opened.IsFailure ? opened.Error.ToString() : null);
        var t = opened.Value;
        Assert.Equal("SF-2026-000001", t.Summary.Number);
        Assert.Equal("352099001761481", t.Summary.SerialNumber); // normalised
        Assert.Equal(new DateOnly(2026, 12, 10), t.Summary.DueDate); // 20 iş günü after Thu 12 Nov
        Assert.True(t.HasUnlockCode);
        Assert.Equal(t.Summary.Id, (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer), "1-4-7-8", "t-1"))).Value.Summary.Id); // retry: same ticket

        // The unlock code is never on a slip, is shown only on request and the viewing is logged.
        var intakeSlip = (await desk.GetSlipAsync(t.Summary.Id, SlipKind.Intake)).Value;
        Assert.Equal("Ekran kırık, dokunmatik çalışmıyor", intakeSlip.Complaint);
        Assert.Equal("Ayşe Kara", intakeSlip.BroughtBy);
        Assert.Contains(intakeSlip.Notes, n => n.Contains("özel servis", StringComparison.Ordinal));
        Assert.DoesNotContain("1-4-7-8", System.Text.Json.JsonSerializer.Serialize(intakeSlip), StringComparison.Ordinal);
        Assert.Equal("1-4-7-8", (await desk.RevealUnlockCodeAsync(t.Summary.Id)).Value);

        // Repair needs an approved price once one was asked.
        await desk.SetDiagnosisAsync(t.Summary.Id, "Ekran ve dokunmatik panel hasarlı", null);
        t = (await desk.SetEstimateAsync(t.Summary.Id, 300_000, 25_000)).Value;
        Assert.Equal(TicketStatus.AwaitingApproval, t.Summary.Status);
        Assert.True(t.Summary.AwaitingApproval);
        Assert.Equal("validation.ticket.approval", (await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Repairing, null, false)).Error.Code);
        t = (await desk.RecordApprovalAsync(t.Summary.Id, true, ApprovalChannel.Phone, "Ayşe Kara")).Value;
        Assert.Equal(TicketStatus.Repairing, t.Summary.Status);

        // Part leaves stock when fitted; removing the line brings it back.
        t = (await desk.AddPartAsync(t.Summary.Id, screen, 1, null)).Value;
        Assert.Equal(2, (await app.Get<InventoryService>().GetProductsAsync()).Value.Single().Stock);
        var wrong = (await desk.AddPartAsync(t.Summary.Id, screen, 1, null)).Value;
        t = (await desk.RemoveLineAsync(t.Summary.Id, wrong.Lines[1].Id)).Value;
        Assert.Equal(2, (await app.Get<InventoryService>().GetProductsAsync()).Value.Single().Stock);
        var staff = (await app.Get<CatalogService>().GetStaffAsync()).Value[0].Id;
        t = (await desk.AddLaborAsync(t.Summary.Id, "Ekran değişimi işçiliği", 50_000, staff)).Value;
        Assert.Equal(300_000, t.AmountDueMinor);

        t = (await desk.TakeDepositAsync(t.Summary.Id, 100_000, PaymentMethod.Cash, "d-1")).Value;
        Assert.Equal(100_000, t.DepositsMinor);
        Assert.Equal("validation.ticket.not_ready", (await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 200_000, 0, 0, "dl-1"))).Error.Code);
        t = (await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, notifyCustomer: true)).Value;
        Assert.Contains(t.History, e => e.Kind == TicketEventKind.MessageQueued && e.Text.Contains("SMS gönderilmedi", StringComparison.Ordinal)); // messaging is off by default

        var delivered = await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 0, 200_000, 0, "dl-1"));
        Assert.True(delivered.IsSuccess, delivered.IsFailure ? delivered.Error.ToString() : null);
        t = delivered.Value;
        Assert.Equal(TicketStatus.Delivered, t.Summary.Status);
        Assert.False(t.HasUnlockCode); // erased at delivery
        Assert.Equal(300_000, t.TotalChargedMinor);
        Assert.Equal(new DateOnly(2027, 11, 12), t.SameFaultWarrantyUntil);
        Assert.Equal(new DateOnly(2027, 5, 12), t.PartsWarrantyUntil);
        Assert.Equal(0, (await app.Get<CustomerService>().GetAsync(customer)).Value.BalanceMinor); // 300.000 charged, 100.000 deposit + 200.000 card
        Assert.Equal(t.Summary.Id, (await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 0, 200_000, 0, "dl-1"))).Value.Summary.Id);
        Assert.Equal("validation.ticket.closed", (await desk.AddLaborAsync(t.Summary.Id, "Sonradan", 1_000, null)).Error.Code);

        var slip = (await desk.GetSlipAsync(t.Summary.Id, SlipKind.Delivery)).Value;
        Assert.Equal(2, slip.Lines.Count);
        Assert.Contains(slip.Notes, n => n.Contains("12 ay", StringComparison.Ordinal) && n.Contains("6 ay", StringComparison.Ordinal));
        Assert.Contains(t.History, e => e.Kind == TicketEventKind.UnlockCodeViewed);
    }

    [Fact]
    public async Task The_same_device_back_within_a_year_is_offered_as_warranty_and_labour_is_free()
    {
        var (app, desk, customer, screen) = await StartAsync();
        await using var _ = app;
        var first = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer), null, "t-1"))).Value.Summary.Id;
        await desk.AddPartAsync(first, screen, 1, null);
        await desk.AddLaborAsync(first, "İşçilik", 50_000, null);
        await desk.ChangeStatusAsync(first, TicketStatus.Ready, null, false);
        await desk.DeliverAsync(new DeliverTicketRequest(first, "Ayşe Kara", 300_000, 0, 0, "dl-1"));

        app.Time.Advance(TimeSpan.FromDays(60));
        var again = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "352099001761481"), null, "t-2"))).Value;
        Assert.NotNull(again.WarrantyCandidate);
        Assert.Equal("SF-2026-000001", again.WarrantyCandidate!.Number);
        Assert.Equal(2, (await desk.GetDeviceHistoryAsync("35 209900 176148 1")).Value.Count);

        var linked = (await desk.LinkWarrantyAsync(again.Summary.Id, again.WarrantyCandidate.TicketId)).Value;
        linked = (await desk.AddLaborAsync(linked.Summary.Id, "İşçilik", 50_000, null)).Value;
        linked = (await desk.AddPartAsync(linked.Summary.Id, screen, 1, 0)).Value;
        Assert.Equal(0, linked.AmountDueMinor);
    }

    [Fact]
    public async Task A_declined_price_sends_the_device_back_with_only_the_agreed_diagnostic_fee()
    {
        var (app, desk, customer, _) = await StartAsync();
        await using var __ = app;
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer), null, "t-1"))).Value;
        await desk.SetEstimateAsync(t.Summary.Id, 400_000, 30_000);
        t = (await desk.RecordApprovalAsync(t.Summary.Id, false, ApprovalChannel.Sms, null)).Value;
        Assert.Equal(TicketStatus.Unrepairable, t.Summary.Status);
        Assert.Equal(30_000, t.AmountDueMinor);
        Assert.Equal("validation.ticket.approval", (await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, false)).Error.Code);
        t = (await desk.DeliverAsync(new DeliverTicketRequest(t.Summary.Id, "Ayşe Kara", 30_000, 0, 0, "dl-1"))).Value;
        Assert.False(t.Repaired);
        Assert.Null(t.SameFaultWarrantyUntil);
        var slip = (await desk.GetSlipAsync(t.Summary.Id, SlipKind.Delivery)).Value;
        Assert.Equal("Müşteri fiyat teklifini onaylamadı.", slip.UnrepairableReason);
        Assert.Equal("validation.ticket.reason", (await desk.ChangeStatusAsync((await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer, "X1"), null, "t-2"))).Value.Summary.Id, TicketStatus.Unrepairable, " ", false)).Error.Code);
    }

    [Fact]
    public async Task Ready_sms_goes_through_the_shops_own_account_when_set_up_and_allowed()
    {
        var (app, desk, customer, _) = await StartAsync();
        await using var __ = app;
        Assert.True((await app.Get<AutoMessagingService>().SaveSettingsAsync(new MessagingSettingsUpdate(true, MessageChannel.Sms, 24, "21:00", "09:00", 10, 30, "SINYAL", "KEY", "HASH", null, null, null, "tr", null))).IsSuccess);
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer), null, "t-1"))).Value;
        t = (await desk.ChangeStatusAsync(t.Summary.Id, TicketStatus.Ready, null, notifyCustomer: true)).Value;
        Assert.Contains(t.History, e => e.Kind == TicketEventKind.MessageQueued && e.Text.Contains("sıraya alındı", StringComparison.Ordinal));
        await using var db = await app.DbAsync();
        var message = await db.Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(MessagePurpose.ServiceReady, message.Purpose);
        Assert.Contains("SF-2026-000001", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Telsa", message.Text, StringComparison.Ordinal); // no device details in the SMS
    }

    [Fact]
    public async Task Intake_needs_the_complaint_and_brand_and_on_site_needs_an_address()
    {
        var (app, desk, customer, _) = await StartAsync();
        await using var __ = app;
        Assert.Equal("validation.ticket.complaint", (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer) with { Complaint = " " }, null, "a"))).Error.Code);
        Assert.Equal("validation.ticket.brand", (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer) with { Brand = "" }, null, "b"))).Error.Code);
        Assert.Equal("validation.ticket.address", (await desk.OpenTicketAsync(new OpenTicketRequest(Phone(customer) with { Device = DeviceKind.AirConditioner, OnSite = true }, null, "c"))).Error.Code);
        Assert.Equal(new DateOnly(2026, 11, 13), ServiceRules.AddBusinessDays(new DateOnly(2026, 11, 12), 1));
        Assert.Equal(new DateOnly(2026, 11, 16), ServiceRules.AddBusinessDays(new DateOnly(2026, 11, 13), 1)); // Friday → Monday
    }
}
