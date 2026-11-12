using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Licensing;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class PlatformTests
{
    [Fact]
    public async Task Data_survives_application_restart()
    {
        var root = Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        Guid customer;
        await using (var app = await TestApp.CreateAsync(root))
        {
            await app.SetupBeautyAsync();
            customer = await app.AddCustomerAsync("İsmail", "Öztürk", "0532 999 88 77");
        }

        await using var restarted = await TestApp.CreateAsync(root);
        Assert.True(await restarted.Get<Vernuko.Application.Services.SetupService>().IsCompletedAsync());
        Assert.True((await restarted.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        var c = await restarted.Get<CustomerService>().GetAsync(customer);
        Assert.Equal("İsmail Öztürk", c.Value.FullName);
    }

    [Fact]
    public async Task Turkish_search_finds_names_and_phone_fragments()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.AddCustomerAsync("Işık", "Çağlar", "0532 456 78 90");
        await app.AddCustomerAsync("Ayşe", "Şahin", "0544 111 22 33");
        var svc = app.Get<CustomerService>();
        Assert.Single((await svc.SearchAsync("isik")).Value);
        Assert.Single((await svc.SearchAsync("IŞIK ÇAĞ")).Value);
        Assert.Single((await svc.SearchAsync("ayse sahin")).Value);
        Assert.Single((await svc.SearchAsync("0532 456")).Value);
        Assert.Single((await svc.SearchAsync("4567890")).Value);
        Assert.Equal(2, (await svc.SearchAsync(null)).Value.Count);
    }

    [Fact]
    public async Task Duplicate_customer_requires_confirmation()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var svc = app.Get<CustomerService>();
        Assert.True((await svc.CreateAsync(new CustomerDetails("Ayşe", "Kara", "0532 456 78 90"), false)).IsSuccess);
        var dup = await svc.CreateAsync(new CustomerDetails("Zeynep", "Kara", "0532 456 78 90"), false);
        Assert.Equal(CustomerErrors.PossibleDuplicateCode, dup.Error.Code);
        Assert.True((await svc.CreateAsync(new CustomerDetails("Zeynep", "Kara", "0532 456 78 90"), true)).IsSuccess);
        Assert.Equal(2, (await svc.SearchAsync("kara")).Value.Count);
    }

    [Fact]
    public async Task Payment_double_submit_creates_one_record()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var payments = app.Get<PaymentService>();
        var request = new RecordTransactionRequest(customer, TransactionKind.Payment, 50_000, PaymentMethod.Cash, true, null, null, "Kapora", "dialog-123");
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => payments.RecordAsync(request))));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Single(results.Select(r => r.Value).Distinct());
        Assert.Equal(-50_000, (await payments.GetBalanceAsync(customer)).Value);
        var day = (await payments.GetCashDayAsync(new DateOnly(2026, 11, 12))).Value;
        Assert.Equal(50_000, day.NetByMethod[PaymentMethod.Cash]);
    }

    [Fact]
    public async Task Financial_records_cannot_be_updated_or_deleted_at_database_level()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var id = (await app.Get<PaymentService>().RecordAsync(new RecordTransactionRequest(customer, TransactionKind.Payment, 1000, PaymentMethod.Card, false, null, null, null, "k"))).Value;
        await using var db = await app.DbAsync();
        var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM CustomerTransactions"));
        Assert.Contains(DatabaseTriggers.AppendOnlyError, ex.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE AuditEntries SET Summary = 'x'"));
        Assert.True((await app.Get<PaymentService>().ReverseAsync(id, "Yanlış kayıt")).IsSuccess);
        Assert.True((await app.Get<PaymentService>().ReverseAsync(id, "İkinci kez")).IsFailure);
    }

    [Fact]
    public async Task Secretary_cannot_see_financial_reports_and_attempt_is_audited()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        Assert.True((await app.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter Hanım", "Randevu-Masasi-1", SystemRole.Secretary)).IsSuccess);
        app.Get<AuthService>().Logout();
        Assert.True((await app.Get<AuthService>().LoginAsync("sekreter", "Randevu-Masasi-1")).IsSuccess);

        Assert.True((await app.Get<CustomerService>().SearchAsync(null)).IsSuccess);
        var report = await app.Get<ReportService>().GetPeriodReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        Assert.True(report.IsSuccess);
        Assert.Null(report.Value.CollectedMinor); // operational report without money figures
        Assert.Equal(Error.Forbidden, (await app.Get<PaymentService>().GetDebtorsAsync()).Error);
        Assert.Equal(Error.Forbidden, (await app.Get<UserAdminService>().ListAsync()).Error);
        Assert.Equal(Error.Forbidden, (await app.Get<AuditService>().GetRecentAsync(10)).Error);

        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        var audit = (await app.Get<AuditService>().GetRecentAsync(100)).Value;
        Assert.Contains(audit, e => e.Action == "access.denied" && e.UserName == "sekreter" && e.Summary == nameof(Permission.ReportsFinancial));
    }

    [Fact]
    public async Task Login_lockout_and_locked_session()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var auth = app.Get<AuthService>();
        auth.Logout();
        for (var i = 0; i < UserAccount.MaxFailedAttemptsBeforeLock - 1; i++)
        {
            Assert.Equal("auth.invalid", (await auth.LoginAsync(TestApp.AdminUser, "yanlis-parola")).Error.Code);
        }

        Assert.Equal("auth.locked", (await auth.LoginAsync(TestApp.AdminUser, "yanlis-parola")).Error.Code);
        Assert.Equal("auth.locked", (await auth.LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).Error.Code);
        app.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.True((await auth.LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);

        auth.Lock();
        Assert.Equal(Error.Forbidden, (await app.Get<CustomerService>().SearchAsync(null)).Error);
        Assert.True((await auth.UnlockAsync("yanlis")).IsFailure);
        Assert.True((await auth.UnlockAsync(TestApp.AdminPassword)).IsSuccess);
        Assert.True((await app.Get<CustomerService>().SearchAsync(null)).IsSuccess);
    }

    [Fact]
    public async Task Audit_chain_verifies_and_detects_tampering()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.AddCustomerAsync();
        var audit = app.Get<AuditService>();
        var ok = (await audit.VerifyChainAsync()).Value;
        Assert.True(ok.IsIntact);
        Assert.True(ok.CheckedEntries >= 3);

        await using (var db = await app.DbAsync())
        {
            // An attacker with file access drops the guard trigger and edits history.
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER trg_AuditEntries_NoUpdate");
            await db.Database.ExecuteSqlRawAsync("UPDATE AuditEntries SET Summary = 'değiştirildi' WHERE Sequence = 2");
        }

        var broken = (await audit.VerifyChainAsync()).Value;
        Assert.False(broken.IsIntact);
        Assert.Equal(2, broken.FirstBrokenSequence);
    }

    [Fact]
    public async Task Expired_license_is_read_only_but_data_stays_readable_and_exportable()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["k1"] = key.ExportSubjectPublicKeyInfo() } };
        await using var app = await TestApp.CreateAsync(licensing: options);
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var now = app.Time.GetUtcNow().UtcDateTime;
        var doc = Domain.LicensingAndMessagingTests.Doc(now.AddDays(30), now.AddDays(7), app.Device.DeviceHash);
        var imported = await app.Get<LicenseService>().ImportAsync(LicenseVerifier.Sign(doc, key, "k1"));
        Assert.True(imported.IsSuccess, imported.IsFailure ? imported.Error.Message : null);
        Assert.Equal(LicenseState.Active, imported.Value.State);

        // Server unreachable for a while: still fully usable inside the grace window.
        app.Time.Advance(TimeSpan.FromDays(10));
        Assert.Equal(LicenseState.OfflineGrace, (await app.Get<LicenseService>().RefreshAsync()).State);
        Assert.True((await app.Get<CustomerService>().CreateAsync(new CustomerDetails("Yeni", "Müşteri", null), true)).IsSuccess);

        app.Time.Advance(TimeSpan.FromDays(25));
        var status = await app.Get<LicenseService>().RefreshAsync();
        Assert.Equal(AccessMode.ReadOnly, status.Mode);
        Assert.Equal(Error.ReadOnlyMode, (await app.Get<CustomerService>().CreateAsync(new CustomerDetails("Engellenen", "Kayıt", null), true)).Error);
        Assert.True((await app.Get<CustomerService>().GetAsync(customer)).IsSuccess);
        Assert.True((await app.Get<CustomerService>().ExportCustomerJsonAsync(customer)).IsSuccess);
        Assert.True((await app.Get<BackupService>().CreateBackupAsync(Vernuko.Backups.BackupTrigger.Manual)).IsSuccess);
    }

    [Fact]
    public async Task Hardware_change_keeps_data_and_switches_to_read_only_until_reactivation()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["k1"] = key.ExportSubjectPublicKeyInfo() } };
        await using var app = await TestApp.CreateAsync(licensing: options);
        await app.SetupBeautyAsync();
        var now = app.Time.GetUtcNow().UtcDateTime;
        await app.Get<LicenseService>().ImportAsync(LicenseVerifier.Sign(Domain.LicensingAndMessagingTests.Doc(now.AddDays(30), now.AddDays(7), app.Device.DeviceHash), key, "k1"));
        app.Device.DeviceHash = "after-windows-reinstall";
        var status = await app.Get<LicenseService>().RefreshAsync();
        Assert.Equal(LicenseState.DeviceMismatch, status.State);
        Assert.True((await app.Get<CustomerService>().SearchAsync(null)).IsSuccess);
    }

    [Fact]
    public async Task Customer_export_and_anonymization()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = (await app.Get<CustomerService>().CreateAsync(new CustomerDetails("Gülşen", "Ünal", "0532 456 78 90", "gulsen@example.com", NationalId: "10000000146"), true)).Value;
        await app.Get<AppointmentService>().CreateAsync(new CreateAppointmentRequest(customer, c.Laser.Id, c.Selin.Id, [c.Room1.Id], TestApp.Local(16), null, "not", null, null), false);

        var json = (await app.Get<CustomerService>().ExportCustomerJsonAsync(customer)).Value;
        Assert.Contains("Gülşen", json, StringComparison.Ordinal);
        Assert.Contains("Lazer Epilasyon", json, StringComparison.Ordinal);
        var csv = (await app.Get<CustomerService>().ExportAllCsvAsync()).Value;
        Assert.StartsWith("﻿No;Ad;Soyad", csv, StringComparison.Ordinal);
        Assert.Contains("Gülşen;Ünal;0532 456 78 90", csv, StringComparison.Ordinal);

        Assert.True((await app.Get<CustomerService>().AnonymizeAsync(customer, "KVKK silme talebi")).IsSuccess);
        var after = (await app.Get<CustomerService>().GetAsync(customer)).Value;
        Assert.Null(after.Phone);
        Assert.Null(after.NationalIdMasked);
        var day = (await app.Get<AppointmentService>().GetDayAsync(new DateOnly(2026, 11, 12), new AppointmentFilter())).Value;
        Assert.Equal(Vernuko.Appointments.AppointmentStatus.Cancelled, day.Items.Single().Status);
    }

    [Fact]
    public async Task Sensitive_national_id_is_masked_without_permission()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var id = (await app.Get<CustomerService>().CreateAsync(new CustomerDetails("Ali", "Veli", null, NationalId: "10000000146"), true)).Value;
        await app.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", SystemRole.Secretary);
        Assert.Equal("10000000146", (await app.Get<CustomerService>().GetAsync(id)).Value.NationalIdMasked);
        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync("sekreter", "Randevu-Masasi-1");
        var dto = (await app.Get<CustomerService>().GetAsync(id)).Value;
        Assert.Equal("*******0146", dto.NationalIdMasked);

        // Saving the form without seeing the id must not erase it.
        Assert.True((await app.Get<CustomerService>().UpdateAsync(id, dto.Version, new CustomerDetails("Ali", "Veli", "0532 111 22 33", NationalId: null), true, true, true, false)).IsSuccess);
        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        Assert.Equal("10000000146", (await app.Get<CustomerService>().GetAsync(id)).Value.NationalIdMasked);
    }

    [Fact]
    public async Task Stale_edit_is_rejected_by_optimistic_concurrency()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var id = await app.AddCustomerAsync();
        var svc = app.Get<CustomerService>();
        var v = (await svc.GetAsync(id)).Value.Version;
        Assert.True((await svc.UpdateAsync(id, v, new CustomerDetails("Ayşe", "Kara", "0532 456 78 90", Notes: "ilk"), true, true, true, false)).IsSuccess);
        var stale = await svc.UpdateAsync(id, v, new CustomerDetails("Ayşe", "Kara", "0532 456 78 90", Notes: "eski ekran"), true, true, true, false);
        Assert.Equal(Error.ConcurrencyConflict, stale.Error);
    }

    [Fact]
    public async Task Failed_migration_rolls_back_to_snapshot_and_newer_database_is_refused()
    {
        var root = Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        string initialMigration;
        await using (var app = await TestApp.CreateAsync(root))
        {
            await app.SetupBeautyAsync();
            await app.AddCustomerAsync("Korunacak", "Kayıt", "0532 456 78 90");
            await using var db = await app.DbAsync();
            initialMigration = db.Database.GetMigrations().Last();

            // Make the schema look "pending" so the migration runs against existing tables and fails.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM __EFMigrationsHistory");
        }

        // Initialisation must fail loudly...
        await Assert.ThrowsAsync<DatabaseMigrationFailedException>(() => TestApp.CreateAsync(root));

        SqliteSetup.ReleaseFileHandles();
        var dbPath = Path.Combine(root, "vernuko.db");
        await using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM Customers WHERE FirstName = 'Korunacak'";
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!); // ...and leave the data intact.
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "safety"), "premigration-*.db"));

            await using var fix = conn.CreateCommand();
            fix.CommandText = "INSERT INTO __EFMigrationsHistory VALUES ($m, '10.0.15'), ('29991231000000_FromTheFuture', '99.0');";
            fix.Parameters.AddWithValue("$m", initialMigration);
            await fix.ExecuteNonQueryAsync();
        }

        // A database written by a newer Vernuko version is never opened (downgrade protection).
        await Assert.ThrowsAsync<DatabaseTooNewException>(() => TestApp.CreateAsync(root));
    }

    [Fact]
    public async Task Search_stays_fast_with_twenty_thousand_customers()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var first = new[] { "Ayşe", "Fatma", "Emine", "Zeynep", "Elif", "Merve", "Selin", "Derya", "Gül", "Işıl" };
        var last = new[] { "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Öztürk", "Aydın", "Özdemir", "Arslan" };
        await using (var db = await app.DbAsync())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            for (var i = 0; i < 20_000; i++)
            {
                db.Customers.Add(Customer.Create(i + 100, new CustomerDetails(first[i % 10], last[(i / 10) % 10] + (i / 100), $"05{(30 + (i % 20)):00} {i % 1000:000} {(i / 1000) % 100:00} {i % 100:00}")).Value);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        var svc = app.Get<CustomerService>();
        await svc.SearchAsync("isil"); // warm-up
        var sw = Stopwatch.StartNew();
        var r = await svc.SearchAsync("işıl yılmaz");
        sw.Stop();
        Assert.NotEmpty(r.Value);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Search took {sw.ElapsedMilliseconds} ms");
        TestContext.Current.SendDiagnosticMessage($"20k customers: search took {sw.ElapsedMilliseconds} ms");
    }
}
