using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Messaging;
using Vernuko.Payments;
using Vernuko.Security;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Regression tests for the defects found in the general bug review (Kasım 2026).</summary>
public class ReviewFixesTests
{
    [Theory]
    [InlineData("1.250,50", 125050)]
    [InlineData("1250,5", 125050)]
    [InlineData("1250", 125000)]
    [InlineData("1.250", 125000)]
    [InlineData("1.250.000", 125000000)]
    [InlineData("12.50", 1250)] // dot as decimal point: was read as ₺1.250,00
    [InlineData("1.5", 150)]
    [InlineData("₺ 2.500,00", 250000)]
    [InlineData("2 500", 250000)]
    public void Money_parse_reads_turkish_input_without_silently_ignoring_dots(string text, long minor) =>
        Assert.Equal(minor, Money.Parse(text).Value.Minor);

    [Theory]
    [InlineData("1.5.0")]
    [InlineData("12.5000")]
    [InlineData("12,50,1")]
    [InlineData("abc")]
    public void Money_parse_rejects_ambiguous_input(string text) => Assert.True(Money.Parse(text).IsFailure);

    [Fact]
    public void A_wait_that_reaches_the_end_of_a_shortened_service_is_ignored_instead_of_throwing()
    {
        // "Saç Boyama" (wait 30+40) shortened to 70 minutes, no cleanup: no work left after the wait.
        var slot = new AppointmentSlot(new DateTime(2026, 11, 13, 7, 0, 0, DateTimeKind.Utc), 70, 0, 0, Guid.NewGuid(), [], 30, 40);
        Assert.False(slot.HasProcessing);
        Assert.Single(slot.Blocks());
    }

    private static MessagingSettingsUpdate Messaging(bool enabled = true, MessageChannel channel = MessageChannel.Sms) =>
        new(enabled, channel, 24, "21:00", "09:00", 10, 30, "ISIL", "KEY", "HASH", "106540352242922", "TOKEN", "randevu_hatirlatma", "tr", null);

    private static async Task<Guid> BookAsync(TestApp app, Guid customer, Catalog c, DateTime local, Guid? package = null) =>
        (await app.Get<AppointmentService>().CreateAsync(new CreateAppointmentRequest(customer, c.Laser.Id, c.Selin.Id, [c.Room1.Id], local, null, null, null, package), confirmWarnings: true)).Value.AppointmentId!.Value;

    [Fact]
    public async Task A_cancelled_reminder_is_planned_again_when_it_is_wanted_again()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync("Ayşe", "Kara", "0532 111 22 33");
        var c = await app.CatalogAsync();
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Messaging())).IsSuccess);
        var appt = await BookAsync(app, customer, c, TestApp.Local(15, 0, day: 13));
        Assert.Equal(1, await svc.PlanRemindersAsync());

        // Switched off (pending one cancelled) and on again: a new reminder, not silence.
        Assert.True((await svc.SaveSettingsAsync(Messaging(enabled: false))).IsSuccess);
        await svc.PlanRemindersAsync();
        Assert.True((await svc.SaveSettingsAsync(Messaging())).IsSuccess);
        Assert.Equal(1, await svc.PlanRemindersAsync());

        // Cancelled, then the cancellation undone: planned again.
        var appointments = app.Get<AppointmentService>();
        Assert.True((await appointments.ChangeStatusAsync(appt, AppointmentStatus.Cancelled, "Müşteri aradı")).IsSuccess);
        await svc.PlanRemindersAsync();
        Assert.True((await appointments.ChangeStatusAsync(appt, AppointmentStatus.Created, "Geri geldi")).IsSuccess);
        Assert.Equal(1, await svc.PlanRemindersAsync());
        Assert.Equal(1, (await svc.GetOutboxAsync()).Value.Count(m => m.Status == OutboxStatus.Pending));
    }

    [Fact]
    public async Task Switching_the_reminder_channel_never_sends_both()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync("Ayşe", "Kara", "0532 111 22 33");
        var c = await app.CatalogAsync();
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Messaging())).IsSuccess);
        await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13)); // due now
        Assert.Equal(1, await svc.PlanRemindersAsync());

        Assert.True((await svc.SaveSettingsAsync(Messaging(channel: MessageChannel.WhatsApp))).IsSuccess);
        await svc.PlanRemindersAsync();
        await svc.DispatchDueAsync();
        var sent = Assert.Single(app.Get<FakeTransports>().Sent);
        Assert.Equal(MessageChannel.WhatsApp, sent.Channel);
    }

    [Fact]
    public async Task The_test_message_is_not_sent_in_read_only_mode()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Messaging())).IsSuccess);
        app.Time.Advance(TimeSpan.FromDays(400)); // trial long over → read-only
        await app.Get<ILicenseService>().RefreshAsync();
        Assert.Equal(Error.ReadOnlyMode.Code, (await svc.SendTestAsync(MessageChannel.Sms, "0532 111 22 33")).Error.Code);
        Assert.Empty(app.Get<FakeTransports>().Sent);
    }

    [Fact]
    public async Task A_package_session_cannot_be_booked_after_the_package_expires()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var c = await app.CatalogAsync();
        var sold = await app.Get<PackageService>().SellAsync(new SellPackageRequest(customer, c.Laser.Id, null, "Lazer 4 seans", null, 4, 400_000, 30, 0, null, "p-30"));
        Assert.True(sold.IsSuccess);

        var late = await app.Get<AppointmentService>().CreateAsync(
            new CreateAppointmentRequest(customer, c.Laser.Id, c.Selin.Id, [c.Room1.Id], TestApp.Local(10, 0, day: 12).AddDays(45), null, null, null, sold.Value), confirmWarnings: true);
        Assert.True(late.IsSuccess);
        Assert.Null(late.Value.AppointmentId);
        Assert.Contains(late.Value.Issues, i => i.Code == "package.expires_before");

        // Inside the validity it is fine.
        Assert.NotEqual(Guid.Empty, await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 20), sold.Value));
    }

    [Fact]
    public async Task A_free_package_sold_twice_with_the_same_key_is_one_package()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var c = await app.CatalogAsync();
        var packages = app.Get<PackageService>();
        var request = new SellPackageRequest(customer, c.Laser.Id, null, "Hediye 2 seans", null, 2, 0, 90, 0, null, "free-1");
        var first = await packages.SellAsync(request);
        var second = await packages.SellAsync(request);
        Assert.Equal(first.Value, second.Value);
        await using var db = await app.DbAsync();
        Assert.Equal(1, await db.CustomerPackages.CountAsync());

        Assert.Equal("validation.package.validity", (await packages.SellAsync(request with { ValidityDays = 0, IdempotencyKey = "free-2" })).Error.Code);
    }

    [Fact]
    public async Task Unlocking_the_lock_screen_has_the_same_lock_out_as_signing_in()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var session = app.Get<UserSession>();
        var auth = app.Get<AuthService>();
        session.Lock();
        Result last = Result.Success();
        for (var i = 0; i < 10 && session.IsAuthenticated; i++)
        {
            last = await auth.UnlockAsync("yanlis-parola");
        }

        Assert.Equal(AppErrors.AccountLocked.Code, last.Error.Code);
        Assert.False(session.IsAuthenticated); // back to the login screen, which keeps the lock-out
        await using var db = await app.DbAsync();
        Assert.Contains(await db.AuditEntries.Select(a => a.Action).ToListAsync(), a => a == "auth.unlock_failed");
    }

    [Fact]
    public async Task Staff_do_not_see_the_customer_balance_on_the_card()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var c = await app.CatalogAsync();
        Assert.True((await app.Get<PackageService>().SellAsync(new SellPackageRequest(customer, c.Laser.Id, null, "Lazer", null, 4, 400_000, 365, 0, null, "p-1"))).IsSuccess);
        Assert.Equal(400_000, (await app.Get<CustomerService>().GetAsync(customer)).Value.BalanceMinor);

        Assert.True((await app.Get<UserAdminService>().CreateAsync("personel", "Personel", "Randevu-Masasi-7", SystemRole.Staff)).IsSuccess);
        var auth = app.Get<AuthService>();
        auth.Logout();
        Assert.True((await auth.LoginAsync("personel", "Randevu-Masasi-7")).IsSuccess);
        Assert.Equal(0, (await app.Get<CustomerService>().GetAsync(customer)).Value.BalanceMinor);
    }

    [Fact]
    public async Task Anonymizing_removes_the_phone_and_text_of_messages_and_the_free_text_of_past_appointments()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync("Ayşe", "Kara", "0532 111 22 33");
        var c = await app.CatalogAsync();
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Messaging())).IsSuccess);
        var appt = await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13));
        Assert.True((await app.Get<AppointmentService>().UpdateNotesAsync(appt, "Sırt bölgesi hassas")).IsSuccess);
        Assert.Equal(1, await svc.PlanRemindersAsync());
        Assert.Equal(1, await svc.DispatchDueAsync());

        Assert.True((await app.Get<CustomerService>().AnonymizeAsync(customer, "KVKK talebi")).IsSuccess);
        await using var db = await app.DbAsync();
        var message = await db.Outbox.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("905321112233", message.Recipient, StringComparison.Ordinal);
        Assert.DoesNotContain("Ayşe", message.Text, StringComparison.Ordinal);
        Assert.Null((await db.Appointments.AsNoTracking().SingleAsync()).Notes);
    }

    [Fact]
    public async Task A_pasted_international_number_finds_the_customer()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.AddCustomerAsync("Ayşe", "Kara", "0532 456 78 90");
        var customers = app.Get<CustomerService>();
        Assert.Single((await customers.SearchAsync("+90 532 456 78 90")).Value);
        Assert.Single((await customers.SearchAsync("0 (532) 456-78-90")).Value);
        Assert.Single((await customers.SearchAsync("Ayşe 532")).Value);
    }

    [Fact]
    public async Task Reversing_a_payment_made_with_a_voided_gift_card_does_not_refill_the_card()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var buyer = await app.AddCustomerAsync();
        var cards = app.Get<GiftCardService>();
        var sold = await cards.SellAsync(new SellGiftCardRequest(buyer, 100_000, PaymentMethod.Cash, null, 365, "gc-1"));
        Assert.True(sold.IsSuccess, sold.IsFailure ? sold.Error.ToString() : null);
        var card = sold.Value;
        var redeemed = await cards.RedeemAsync(new RedeemGiftCardRequest(card.Code, buyer, 60_000, null, "gc-r-1"));
        Assert.True(redeemed.IsSuccess, redeemed.IsFailure ? redeemed.Error.ToString() : null);
        Assert.True((await cards.VoidAsync(card.Id, "Kart iade edildi")).IsSuccess);

        Assert.True((await app.Get<PaymentService>().ReverseAsync(redeemed.Value, "Yanlış müşteri")).IsSuccess);
        await using var db = await app.DbAsync();
        Assert.DoesNotContain(await db.GiftCardEntries.AsNoTracking().Select(e => e.Kind).ToListAsync(), k => k == GiftCardEntryKind.Restore);
        Assert.Equal(0, (await cards.GetAsync(card.Code)).Value.BalanceMinor);
    }
}
