using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class Phase2Tests
{
    private static CreateAppointmentRequest Request(Guid customer, ServiceDto service, StaffDto staff, ResourceDto? room, DateTime local, Guid? package = null) =>
        new(customer, service.Id, staff.Id, room is null ? [] : [room.Id], local, null, null, null, package);

    private static CommissionRuleDto Rule(CommissionBasis basis, Guid? staff, Guid? item, int percentBp = 0, long fixedMinor = 0) =>
        new(Guid.Empty, basis, staff, string.Empty, item, string.Empty, fixedMinor > 0 ? CommissionRateKind.FixedAmount : CommissionRateKind.Percent, percentBp, fixedMinor, true);

    [Fact]
    public async Task Service_commission_uses_most_specific_rule_and_reverses_on_reopen()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var commissions = app.Get<CommissionService>();
        Assert.True((await commissions.SaveRuleAsync(Rule(CommissionBasis.ServiceDelivered, null, null, percentBp: 1000))).IsSuccess);
        Assert.True((await commissions.SaveRuleAsync(Rule(CommissionBasis.ServiceDelivered, c.Selin.Id, null, percentBp: 4000))).IsSuccess);

        var svc = app.Get<AppointmentService>();
        var selinAppt = (await svc.CreateAsync(Request(customer, c.SkinCare, c.Selin, c.Room1, TestApp.Local(10)), false)).Value.AppointmentId!.Value;
        var deryaAppt = (await svc.CreateAsync(Request(customer, c.SkinCare, c.Derya, c.Room2, TestApp.Local(10)), true)).Value.AppointmentId!.Value;
        Assert.True((await svc.ChangeStatusAsync(selinAppt, AppointmentStatus.Completed, null)).IsSuccess);
        Assert.True((await svc.ChangeStatusAsync(deryaAppt, AppointmentStatus.Completed, null)).IsSuccess);
        Assert.True((await svc.ChangeStatusAsync(selinAppt, AppointmentStatus.Completed, null)).IsSuccess); // no-op, no double commission

        var report = (await commissions.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        Assert.Equal(60_000, report.Totals.Single(t => t.StaffMemberId == c.Selin.Id).ServiceMinor); // %40 of 1.500 TL
        Assert.Equal(15_000, report.Totals.Single(t => t.StaffMemberId == c.Derya.Id).ServiceMinor); // %10

        Assert.True((await svc.ChangeStatusAsync(selinAppt, AppointmentStatus.InProgress, "yanlışlıkla")).IsSuccess);
        report = (await commissions.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        Assert.Equal(0, report.Totals.Single(t => t.StaffMemberId == c.Selin.Id).ServiceMinor);
        Assert.True((await svc.ChangeStatusAsync(selinAppt, AppointmentStatus.Completed, null)).IsSuccess);
        report = (await commissions.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        Assert.Equal(60_000, report.Totals.Single(t => t.StaffMemberId == c.Selin.Id).ServiceMinor);

        // The commission ledger cannot be edited at database level.
        await using var db = await app.DbAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE CommissionEntries SET AmountMinor = 0"));
    }

    [Fact]
    public async Task Package_seller_and_session_performer_are_commissioned_separately()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var commissions = app.Get<CommissionService>();
        await commissions.SaveRuleAsync(Rule(CommissionBasis.PackageSold, null, null, fixedMinor: 10_000));
        await commissions.SaveRuleAsync(Rule(CommissionBasis.ServiceDelivered, null, null, percentBp: 2000));

        var pkg = (await app.Get<PackageService>().SellAsync(new SellPackageRequest(customer, c.Laser.Id, null, "Lazer 8", null, 8, 800_000, 365, 0, null, "sale", c.Derya.Id))).Value;
        var appt = (await app.Get<AppointmentService>().CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(10, 30), pkg), false)).Value.AppointmentId!.Value;
        app.Time.Advance(TimeSpan.FromHours(1));
        Assert.True((await app.Get<AppointmentService>().ChangeStatusAsync(appt, AppointmentStatus.Completed, null)).IsSuccess);

        var report = (await commissions.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        Assert.Equal(10_000, report.Totals.Single(t => t.StaffMemberId == c.Derya.Id).PackageSaleMinor);
        Assert.Equal(20_000, report.Totals.Single(t => t.StaffMemberId == c.Selin.Id).ServiceMinor); // %20 of one session (8.000/8 = 1.000 TL)

        Assert.True((await app.Get<PackageService>().CancelAsync(pkg, "Müşteri vazgeçti")).IsSuccess);
        report = (await commissions.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        Assert.Equal(0, report.Totals.Single(t => t.StaffMemberId == c.Derya.Id).PackageSaleMinor);
    }

    [Fact]
    public async Task Stock_is_consumed_by_services_restored_on_reopen_and_sold_to_customers()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var inv = app.Get<InventoryService>();
        var gel = (await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Lazer jeli", null, "ml", 0, 5, 100, true, 0))).Value;
        var cream = (await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Nemlendirici krem", "KR-1", "adet", 25_000, 12_000, 2, true, 0))).Value;
        Assert.Equal(500, (await inv.ReceiveAsync(gel, 500, 5, "Tedarikçi faturası 123")).Value);
        Assert.Equal(5, (await inv.ReceiveAsync(cream, 5, 12_000, null)).Value);
        Assert.True((await inv.SetServiceMaterialsAsync(c.Laser.Id, [(gel, 20)])).IsSuccess);

        var svc = app.Get<AppointmentService>();
        var appt = (await svc.CreateAsync(Request(customer, c.Laser, c.Selin, c.Room1, TestApp.Local(10)), false)).Value.AppointmentId!.Value;
        await svc.ChangeStatusAsync(appt, AppointmentStatus.Completed, null);
        Assert.Equal(480, (await inv.GetProductsAsync()).Value.Single(p => p.Id == gel).Stock);
        await svc.ChangeStatusAsync(appt, AppointmentStatus.InProgress, "geri al");
        Assert.Equal(500, (await inv.GetProductsAsync()).Value.Single(p => p.Id == gel).Stock);

        Assert.True((await inv.AdjustAsync(gel, -10, " ")).IsFailure);
        Assert.Equal(490, (await inv.AdjustAsync(gel, -10, "Sayım farkı")).Value);

        var sale = new SellProductRequest(customer, cream, 4, 25_000, c.Derya.Id, PaymentMethod.Cash, "product-dialog-1");
        var first = await inv.SellAsync(sale);
        var second = await inv.SellAsync(sale);
        Assert.Equal(first.Value, second.Value);
        var creamDto = (await inv.GetProductsAsync()).Value.Single(p => p.Id == cream);
        Assert.Equal(1, creamDto.Stock);
        Assert.True(creamDto.IsLow);
        Assert.Equal(0, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);
        Assert.NotEmpty((await inv.GetMovementsAsync(cream)).Value);

        await using var db = await app.DbAsync();
        var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM StockMovements"));
        Assert.Contains(DatabaseTriggers.AppendOnlyError, ex.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE StockMovements SET QuantityDelta = 1000"));
    }

    [Fact]
    public async Task Weekly_series_reports_conflicts_skips_them_and_moves_or_cancels_the_rest()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var therapyClient = await app.AddCustomerAsync("Danışan", "Bir", "0532 100 00 00");
        var other = await app.AddCustomerAsync("Diğer", "Müşteri", "0532 200 00 00");
        var svc = app.Get<AppointmentService>();
        // Week 3 (26 Nov) is already taken.
        Assert.True((await svc.CreateAsync(Request(other, c.SkinCare, c.Selin, c.Room1, TestApp.Local(15, day: 26)), false)).Value.Saved);

        var pattern = new RecurrencePattern(RecurrenceFrequency.Weekly, 1, 4);
        var request = Request(therapyClient, c.SkinCare, c.Selin, c.Room1, TestApp.Local(15, day: 12));
        var attempt = (await svc.CreateSeriesAsync(request, pattern, confirmWarnings: false, skipBlocked: false)).Value;
        Assert.False(attempt.Saved);
        Assert.Contains(attempt.Problems, p => p.StartLocal.Day == 26 && p.Issues.Any(i => i.Severity == IssueSeverity.Blocking));

        var created = (await svc.CreateSeriesAsync(request, pattern, confirmWarnings: true, skipBlocked: true)).Value;
        Assert.True(created.Saved);
        Assert.Equal([12, 19, 3], created.CreatedLocal.Select(d => d.Day).ToArray());

        var range = (await svc.GetRangeAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 31), new AppointmentFilter())).Value.Where(i => i.CustomerId == therapyClient).ToList();
        Assert.All(range, i => Assert.Equal(created.SeriesId, i.SeriesId));

        // Move the first one by one week: every later one moves too, even onto the old slot of its successor.
        var moved = await svc.RescheduleAsync(new RescheduleRequest(range[0].Id, TestApp.Local(17, day: 12), null, null, ApplyToFollowing: true), true);
        Assert.True(moved.Value.Saved, string.Join("; ", moved.Value.Issues.Select(i => i.Message)));
        range = (await svc.GetRangeAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 31), new AppointmentFilter())).Value.Where(i => i.CustomerId == therapyClient).ToList();
        Assert.All(range, i => Assert.Equal(17, i.StartLocal.Hour));

        // A shift that would collide with another customer's booking (26 Nov 15:00 + cleanup until 16:10) is refused as a whole.
        var collide = await svc.RescheduleAsync(new RescheduleRequest(range[0].Id, TestApp.Local(15, day: 19), null, null, ApplyToFollowing: true), true);
        Assert.False(collide.Value.Saved);
        Assert.Contains(collide.Value.Issues, i => i.Message.StartsWith("26.11.2026", StringComparison.Ordinal));

        var weekShift = await svc.RescheduleAsync(new RescheduleRequest(range[0].Id, TestApp.Local(17, day: 19), null, null, ApplyToFollowing: true), true);
        Assert.True(weekShift.Value.Saved, string.Join("; ", weekShift.Value.Issues.Select(i => i.Message)));

        range = (await svc.GetRangeAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 31), new AppointmentFilter())).Value.Where(i => i.CustomerId == therapyClient).OrderBy(i => i.StartLocal).ToList();
        Assert.Equal(2, (await svc.CancelSeriesFromAsync(range[1].Id, "Danışan ara verdi")).Value);
        range = (await svc.GetRangeAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 31), new AppointmentFilter())).Value.Where(i => i.CustomerId == therapyClient).OrderBy(i => i.StartLocal).ToList();
        Assert.Equal(AppointmentStatus.Created, range[0].Status);
        Assert.All(range.Skip(1), i => Assert.Equal(AppointmentStatus.Cancelled, i.Status));
    }

    [Fact]
    public async Task Recurrence_keeps_local_time_and_month_end()
    {
        var weekly = new RecurrencePattern(RecurrenceFrequency.Weekly, 2, 3).Occurrences(new DateTime(2026, 3, 22, 10, 0, 0));
        Assert.Equal([new DateTime(2026, 3, 22, 10, 0, 0), new DateTime(2026, 4, 5, 10, 0, 0), new DateTime(2026, 4, 19, 10, 0, 0)], weekly);
        var monthly = new RecurrencePattern(RecurrenceFrequency.Monthly, 1, 3).Occurrences(new DateTime(2026, 1, 31, 9, 0, 0));
        Assert.Equal(28, monthly[1].Day);
        Assert.True(new RecurrencePattern(RecurrenceFrequency.Weekly, 1, 1).Validate().IsFailure);
        Assert.True(new RecurrencePattern(RecurrenceFrequency.Weekly, 1, 53).Validate().IsFailure);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Waitlist_matches_freed_slot_after_cancellation()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var booked = await app.AddCustomerAsync();
        var waiting = await app.AddCustomerAsync("Zeynep", "Ak", "0551 234 56 78");
        var unrelated = await app.AddCustomerAsync("Başka", "Hizmet", "0551 000 00 00");
        var wl = app.Get<WaitlistService>();
        var entry = (await wl.AddAsync(waiting, c.Laser.Id, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 20), "öğleden sonra", null)).Value;
        await wl.AddAsync(unrelated, c.SkinCare.Id, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 20), null, null);
        Assert.True((await wl.AddAsync(waiting, null, null, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 10), null, null)).IsFailure);

        var appt = (await app.Get<AppointmentService>().CreateAsync(Request(booked, c.Laser, c.Selin, c.Room1, TestApp.Local(15)), false)).Value.AppointmentId!.Value;
        await app.Get<AppointmentService>().ChangeStatusAsync(appt, AppointmentStatus.Cancelled, "iptal");
        var matches = (await wl.FindMatchesForAsync(appt)).Value;
        Assert.Equal("Zeynep Ak", Assert.Single(matches).CustomerName);

        Assert.True((await wl.SetStatusAsync(entry, WaitlistStatus.Booked, appt)).IsSuccess);
        Assert.Empty((await wl.FindMatchesForAsync(appt)).Value);
        Assert.Single((await wl.ListAsync()).Value);
        Assert.Equal(2, (await wl.ListAsync(includeClosed: true)).Value.Count);
    }

    [Fact]
    public async Task Consents_and_encrypted_photos_with_permissions_backup_and_anonymization()
    {
        await using var app = await TestApp.CreateAsync();
        var key = (await app.SetupBeautyAsync()).RecoveryKey!;
        var customer = await app.AddCustomerAsync("Fotoğraf", "Müşterisi", "0532 777 77 77");
        var records = app.Get<CustomerRecordsService>();
        var template = (await records.SaveTemplateAsync(new ConsentTemplateDto(Guid.Empty, "Lazer bilgilendirme ve onam", "Metin hukuk danışmanınızla hazırlanmalıdır.", 0, true))).Value;
        var photo = Enumerable.Range(0, 5000).Select(i => (byte)(i % 251)).ToArray();
        photo[0] = 0xFF;
        photo[1] = 0xD8; // looks like a JPEG
        var fileId = (await records.AddFileAsync(customer, CustomerFileKind.Photo, "once.jpg", photo, "1. seans öncesi")).Value;
        Assert.Equal(photo, (await records.ReadFileAsync(fileId)).Value);

        // Encrypted on disk: plain bytes are not present in the stored blob.
        var blob = await File.ReadAllBytesAsync(Directory.GetFiles(Path.Combine(app.Root, "attachments")).Single());
        Assert.False(blob.AsSpan().IndexOf(photo.AsSpan(100, 64)) >= 0);
        Assert.True((await records.AddFileAsync(customer, CustomerFileKind.Photo, "virus.exe", photo, null)).IsFailure);

        // Secretary may record consent but cannot see photos.
        await app.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", SystemRole.Secretary);
        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync("sekreter", "Randevu-Masasi-1");
        Assert.True((await records.RecordConsentAsync(customer, template, ConsentMethod.PaperSigned, "Arşiv klasörü 3")).IsSuccess);
        Assert.Equal(Error.Forbidden, (await records.GetFilesAsync(customer)).Error);
        Assert.Equal(Error.Forbidden, (await records.ReadFileAsync(fileId)).Error);
        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        var consent = Assert.Single((await records.GetConsentsAsync(customer)).Value);
        Assert.Equal(1, consent.TemplateVersion);

        // Backup → restore on a new computer: photos come back and can be decrypted there.
        var backup = (await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual)).Value;
        await using (var newPc = await TestApp.CreateAsync(protector: new TestSecretProtector()))
        {
            await newPc.SetupBeautyAsync(enableBackup: false);
            var restored = await newPc.Get<BackupService>().RestoreAsync(Path.Combine(app.Root, "backups", backup.FileName), key);
            Assert.True(restored.IsSuccess, restored.IsFailure ? restored.Error.Message : null);
            await newPc.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
            Assert.Equal(photo, (await newPc.Get<CustomerRecordsService>().ReadFileAsync(fileId)).Value);
        }

        // Anonymisation removes the photos.
        Assert.True((await app.Get<CustomerService>().AnonymizeAsync(customer, "KVKK silme talebi")).IsSuccess);
        Assert.Empty((await records.GetFilesAsync(customer)).Value);
        Assert.Empty(Directory.GetFiles(Path.Combine(app.Root, "attachments")));
    }

    [Fact]
    public async Task Attachment_crypto_detects_tampering_and_wrong_file()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var data = AttachmentCrypto.Encrypt(key, id, [1, 2, 3, 4]);
        Assert.Equal([1, 2, 3, 4], AttachmentCrypto.Decrypt(key, id, data));
        Assert.Null(AttachmentCrypto.Decrypt(key, Guid.NewGuid(), data));
        data[^1] ^= 1;
        Assert.Null(AttachmentCrypto.Decrypt(key, id, data));
        await Task.CompletedTask;
    }
}
