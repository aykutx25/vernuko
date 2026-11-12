using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class AppointmentFlowTests
{
    private static CreateAppointmentRequest Request(Guid customer, ServiceDto service, StaffDto staff, ResourceDto? room, DateTime local, Guid? package = null) =>
        new(customer, service.Id, staff.Id, room is null ? [] : [room.Id], local, null, null, null, package);

    [Fact]
    public async Task Two_appointments_for_the_same_staff_and_time_cannot_both_be_saved()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var b = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        var svc = app.Get<AppointmentService>();

        var first = await svc.CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(14)), false);
        Assert.True(first.Value.Saved);
        var second = await svc.CreateAsync(Request(b, c.Laser, c.Selin, c.Room2, TestApp.Local(14, 15)), true);
        Assert.False(second.Value.Saved);
        Assert.Contains(second.Value.Issues, i => i.Code == "conflict.staff" && i.Severity == IssueSeverity.Blocking);
    }

    [Fact]
    public async Task Room_and_device_conflicts_are_blocked_including_cleanup_buffer()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var b = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        var svc = app.Get<AppointmentService>();

        Assert.True((await svc.CreateAsync(new CreateAppointmentRequest(a, c.Laser.Id, c.Selin.Id, [c.Room1.Id, c.LaserDevice.Id], TestApp.Local(14), null, null, null, null), false)).Value.Saved);

        // Same room, other staff, starts during the 10-minute cleanup of the first appointment.
        var room = await svc.CreateAsync(Request(b, c.Laser, c.Derya, c.Room1, TestApp.Local(14, 35)), true);
        Assert.Contains(room.Value.Issues, i => i.Code == "conflict.resource");

        // Same device, other room and staff.
        var device = await svc.CreateAsync(new CreateAppointmentRequest(b, c.Laser.Id, c.Derya.Id, [c.Room2.Id, c.LaserDevice.Id], TestApp.Local(14, 15), null, null, null, null), true);
        Assert.Contains(device.Value.Issues, i => i.Code == "conflict.resource");

        // After the buffer it is fine.
        Assert.True((await svc.CreateAsync(Request(b, c.Laser, c.Derya, c.Room1, TestApp.Local(14, 40)), false)).Value.Saved);
    }

    [Fact]
    public async Task Staff_on_leave_cannot_be_booked()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var leave = await app.Get<CatalogService>().AddExceptionAsync(ScheduleExceptionScope.Staff, c.Selin.Id, ScheduleExceptionKind.Leave, TestApp.Local(0, day: 13), TestApp.Local(23, 59, day: 13), "Yıllık izin");
        Assert.True(leave.IsSuccess);
        var r = await app.Get<AppointmentService>().CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(11, day: 13)), true);
        Assert.False(r.Value.Saved);
        Assert.Contains(r.Value.Issues, i => i.Code == "staff.leave");
    }

    [Fact]
    public async Task Warnings_need_explicit_confirmation()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var svc = app.Get<AppointmentService>();
        var lunch = await svc.CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(12, 45)), false);
        Assert.True(lunch.Value.NeedsConfirmation);
        Assert.True((await svc.CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(12, 45)), true)).Value.Saved);
    }

    [Fact]
    public async Task Laser_package_counts_down_only_on_completion_and_cancellation_keeps_the_session()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var packages = app.Get<PackageService>();
        var sold = await packages.SellAsync(new SellPackageRequest(customer, c.Laser.Id, null, "Lazer Epilasyon 8 Seans", "Bacak", 8, 800_000, 365, 200_000, PaymentMethod.Card, "sale-1"));
        Assert.True(sold.IsSuccess, sold.IsFailure ? sold.Error.Message : null);
        var svc = app.Get<AppointmentService>();

        var first = (await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(10, 30), sold.Value), false)).Value;
        var second = (await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(15), sold.Value), false)).Value;
        Assert.True(first.Saved && second.Saved);

        app.Time.Advance(TimeSpan.FromMinutes(40));
        Assert.True((await svc.ChangeStatusAsync(first.AppointmentId!.Value, AppointmentStatus.InProgress, null)).IsSuccess);
        Assert.True((await svc.ChangeStatusAsync(first.AppointmentId!.Value, AppointmentStatus.Completed, null)).IsSuccess);
        Assert.True((await svc.ChangeStatusAsync(second.AppointmentId!.Value, AppointmentStatus.Cancelled, "Müşteri iptal etti")).IsSuccess);

        var pkg = (await packages.GetCustomerPackagesAsync(customer)).Value.Single();
        Assert.Equal(7, pkg.RemainingSessions);
        Assert.Equal(1, pkg.UsedSessions);
        Assert.Equal(0, pkg.UpcomingBooked);

        // Package price was charged once; 2.000 TL paid → 6.000 TL open balance. No per-appointment charge.
        Assert.Equal(600_000, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);

        // Reopening the completed appointment gives the session back.
        Assert.True((await svc.ChangeStatusAsync(first.AppointmentId!.Value, AppointmentStatus.InProgress, "Yanlışlıkla tamamlandı")).IsSuccess);
        Assert.Equal(8, (await packages.GetCustomerPackagesAsync(customer)).Value.Single().RemainingSessions);

        var ledger = (await packages.GetLedgerAsync(pkg.Id)).Value;
        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    public async Task Non_package_completion_charges_once_and_reopen_reverses()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var svc = app.Get<AppointmentService>();
        var id = (await svc.CreateAsync(Request(customer, c.SkinCare, c.Derya, c.Room2, TestApp.Local(10)), false)).Value.AppointmentId!.Value;
        Assert.True((await svc.ChangeStatusAsync(id, AppointmentStatus.Completed, null)).IsSuccess);
        Assert.Equal(150_000, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);
        Assert.True((await svc.ChangeStatusAsync(id, AppointmentStatus.InProgress, "düzeltme")).IsSuccess);
        Assert.Equal(0, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);
        Assert.True((await svc.ChangeStatusAsync(id, AppointmentStatus.Completed, null)).IsSuccess);
        Assert.Equal(150_000, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);
    }

    [Fact]
    public async Task Future_appointment_cannot_be_completed()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var svc = app.Get<AppointmentService>();
        var id = (await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(16)), false)).Value.AppointmentId!.Value;
        var r = await svc.ChangeStatusAsync(id, AppointmentStatus.Completed, null);
        Assert.Equal("validation.appointment.not_started", r.Error.Code);
    }

    [Fact]
    public async Task Treatment_may_start_up_to_an_hour_early_but_no_show_only_after_the_start_time()
    {
        // Found on the first Windows run: "Tamamla" was offered for tomorrow's appointment and then refused, and a
        // customer who came 10 minutes early could not be started at all. Test clock: 12 Nov 10:00 local.
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var svc = app.Get<AppointmentService>();

        var soon = (await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(10, 50)), false)).Value.AppointmentId!.Value;
        var noShowEarly = await svc.ChangeStatusAsync(soon, AppointmentStatus.NoShow, null);
        Assert.Contains("Gelmedi", noShowEarly.Error.Message);
        Assert.True((await svc.ChangeStatusAsync(soon, AppointmentStatus.Arrived, null)).IsSuccess);
        Assert.True((await svc.ChangeStatusAsync(soon, AppointmentStatus.Completed, null)).IsSuccess); // 50 min early

        var later = (await svc.CreateAsync(Request(customer, c.Laser, c.Derya, c.Room2, TestApp.Local(12)), false)).Value.AppointmentId!.Value;
        var r = await svc.ChangeStatusAsync(later, AppointmentStatus.Completed, null);
        Assert.Equal("validation.appointment.not_started", r.Error.Code);
        Assert.Contains("1 saat", r.Error.Message);

        // The day list only offers what can be chosen now.
        var page = app.Get<Vernuko.Presentation.Pages.AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 12);
        await page.OnNavigatedToAsync(null);
        var row = page.Rows.OfType<Vernuko.Presentation.Pages.AppointmentRow>().Single(x => x.Item.Id == later);
        Assert.DoesNotContain(row.Actions, a => a.Target is AppointmentStatus.Completed or AppointmentStatus.InProgress or AppointmentStatus.NoShow);
        Assert.Contains(row.Actions, a => a.Target == AppointmentStatus.Arrived);
    }

    [Fact]
    public async Task Reschedule_moves_bookings_and_frees_old_slot()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var b = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        var svc = app.Get<AppointmentService>();
        var id = (await svc.CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(14)), false)).Value.AppointmentId!.Value;

        // Overlaps its own old slot: must not conflict with itself.
        var moved = await svc.RescheduleAsync(new RescheduleRequest(id, TestApp.Local(14, 15), null, null), false);
        Assert.True(moved.Value.Saved, string.Join(", ", moved.Value.Issues.Select(i => i.Code)));
        Assert.True((await svc.CreateAsync(Request(b, c.Laser, c.Selin, c.Room2, TestApp.Local(13, 30)), false)).Value.Saved);
        var day = (await svc.GetDayAsync(new DateOnly(2026, 11, 12), new AppointmentFilter())).Value;
        Assert.Equal(new TimeOnly(14, 15), TimeOnly.FromDateTime(day.Items.Single(i => i.Id == id).StartLocal));
    }

    [Fact]
    public async Task Undo_cancel_rechecks_conflicts()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var a = await app.AddCustomerAsync();
        var b = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        var svc = app.Get<AppointmentService>();
        var first = (await svc.CreateAsync(Request(a, c.Laser, c.Selin, c.Room1, TestApp.Local(14)), false)).Value.AppointmentId!.Value;
        Assert.True((await svc.ChangeStatusAsync(first, AppointmentStatus.Cancelled, "iptal")).IsSuccess);
        Assert.True((await svc.CreateAsync(Request(b, c.Laser, c.Selin, c.Room1, TestApp.Local(14)), false)).Value.Saved);
        var undo = await svc.ChangeStatusAsync(first, AppointmentStatus.Created, null);
        Assert.Equal("conflict.reactivate", undo.Error.Code);
    }

    [Fact]
    public async Task Database_trigger_blocks_overlaps_even_if_application_check_is_bypassed()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        Assert.True((await app.Get<AppointmentService>().CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(14)), false)).Value.Saved);

        await using var db = await app.DbAsync();
        var slot = new AppointmentSlot(TimeZones.ToUtc(TestApp.Local(14, 10), TimeZones.Find(TimeZones.Turkey)), 30, 0, 0, c.Selin.Id, []);
        var rogue = Appointment.Create(Vernuko.BusinessProfiles.Branch.DefaultId, customer, c.Laser.Id, slot, 0, AppointmentStatus.Created, AppointmentSource.FrontDesk, null, null, null, null, app.Time.GetUtcNow().UtcDateTime).Value;
        db.Appointments.Add(rogue);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(DatabaseTriggers.OverlapError, ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_bookings_for_same_slot_only_one_wins()
    {
        var root = Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        await using var app1 = await TestApp.CreateAsync(root);
        await app1.SetupBeautyAsync();
        var c = await app1.CatalogAsync();
        var customers = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            customers.Add(await app1.AddCustomerAsync($"Müşteri{i}", "Test", $"0532 000 00 0{i}"));
        }

        // A second, independent application stack on the same database file (like a second process).
        await using var app2 = await TestApp.CreateAsync(root);
        Assert.True((await app2.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        await app2.Get<LicenseService>().RefreshAsync();

        var tasks = customers.Select((cust, i) =>
        {
            var app = i % 2 == 0 ? app1 : app2;
            return Task.Run(() => app.Get<AppointmentService>().CreateAsync(Request(cust, c.Laser, c.Selin, c.Room1, TestApp.Local(17)), true));
        }).ToList();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(r => r.IsSuccess && r.Value.Saved));
        var day = (await app1.Get<AppointmentService>().GetDayAsync(new DateOnly(2026, 11, 12), new AppointmentFilter())).Value;
        Assert.Single(day.Items);
    }

    [Fact]
    public async Task Day_agenda_has_items_summary_break_and_session_labels()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var pkg = (await app.Get<PackageService>().SellAsync(new SellPackageRequest(customer, c.Laser.Id, null, "Lazer 8", null, 8, 0, null, 0, null, "s"))).Value;
        await app.Get<PackageService>().AdjustSessionsAsync(pkg, -2, "Önceki sistemde kullanılan 2 seans");
        var svc = app.Get<AppointmentService>();
        await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(15), pkg), false);
        await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(15, day: 19), pkg), true);
        var day = (await svc.GetDayAsync(new DateOnly(2026, 11, 12), new AppointmentFilter())).Value;
        Assert.Equal("3. Seans", day.Items.Single().SessionLabel);
        Assert.Equal(1, day.Summary.Total);
        Assert.Equal(1, day.Summary.Open);
        Assert.Equal(new TimeInterval(new TimeOnly(12, 30), new TimeOnly(13, 30)), day.Breaks.Single());
        var week = (await svc.GetRangeAsync(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), new AppointmentFilter())).Value;
        Assert.Equal("4. Seans", week.Single().SessionLabel);
    }
}
