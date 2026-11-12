using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Backups;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>
/// Section 31-I of the brief: the first working version must complete this flow for real
/// (UI steps are covered by ViewModel tests; this runs the same services on a real SQLite file).
/// </summary>
public class SuccessCriteriaTests
{
    [Fact]
    public async Task First_release_flow_end_to_end()
    {
        var root = Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        string recoveryKey;
        Guid customer;
        Guid package;
        string backupFile;

        await using (var app = await TestApp.CreateAsync(root))
        {
            // 1-3. Install, choose "Güzellik ve Lazer Merkezi", define business and staff.
            var profile = BusinessProfileCatalog.Get(BusinessProfileId.BeautyAndLaser);
            Assert.Equal("Güzellik ve Lazer Merkezi", profile.DisplayName);
            Assert.Single(BusinessProfileCatalog.All, p => p.DisplayName.Contains("Lazer", StringComparison.Ordinal));
            recoveryKey = (await app.SetupBeautyAsync()).RecoveryKey!;
            var catalog = app.Get<CatalogService>();
            Assert.True((await catalog.SaveStaffAsync(new StaffDto(Guid.Empty, "Merve Kaya", "Cilt Uzmanı", null, null, true, null, []))).IsSuccess);

            // 4. Add a customer.
            customer = (await app.Get<CustomerService>().CreateAsync(new CustomerDetails("Ayşe", "Kara", "0532 456 78 90"), false)).Value;

            // 5. Create a service and an appointment (with an 8-session laser package).
            var serviceId = (await catalog.SaveServiceAsync(new ServiceDto(Guid.Empty, "Lazer Epilasyon - Tüm Bacak", "Lazer", 45, 0, 10, 200_000, ResourceKind.Room, true))).Value;
            var service = (await catalog.GetServicesAsync()).Value.Single(s => s.Id == serviceId);
            var c = await app.CatalogAsync();
            package = (await app.Get<PackageService>().SellAsync(new SellPackageRequest(customer, serviceId, null, "Tüm Bacak 8 Seans", "Tüm bacak", 8, 1_600_000, 365, 400_000, Vernuko.Payments.PaymentMethod.Cash, Guid.NewGuid().ToString()))).Value;
            var booking = await app.Get<AppointmentService>().CreateAsync(
                new CreateAppointmentRequest(customer, service.Id, c.Selin.Id, [c.Room1.Id], TestApp.Local(11), null, "İlk seans", "Tüm bacak", package), false);
            Assert.True(booking.Value.Saved, string.Join(",", booking.Value.Issues.Select(i => i.Message)));

            // 6. See it on the calendar.
            var day = (await app.Get<AppointmentService>().GetDayAsync(new DateOnly(2026, 11, 12), new AppointmentFilter())).Value;
            var item = day.Items.Single();
            Assert.Equal("Ayşe Kara", item.CustomerName);
            Assert.Equal("1. Seans", item.SessionLabel);
            Assert.Equal("Lazer Odası 1", item.ResourceNames);

            // 7-8. Complete it; the package goes from 8 to 7.
            app.Time.Advance(TimeSpan.FromHours(1.5));
            Assert.True((await app.Get<AppointmentService>().ChangeStatusAsync(item.Id, AppointmentStatus.Arrived, null)).IsSuccess);
            Assert.True((await app.Get<AppointmentService>().ChangeStatusAsync(item.Id, AppointmentStatus.InProgress, null)).IsSuccess);
            Assert.True((await app.Get<AppointmentService>().ChangeStatusAsync(item.Id, AppointmentStatus.Completed, null)).IsSuccess);
            Assert.Equal(7, (await app.Get<PackageService>().GetCustomerPackagesAsync(customer)).Value.Single().RemainingSessions);

            // 10. Local encrypted backup.
            var run = await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual);
            Assert.True(run.IsSuccess);
            backupFile = Path.Combine(root, "backups", run.Value.FileName);
        }

        // 9. Close and reopen: data is still there.
        await using (var reopened = await TestApp.CreateAsync(root))
        {
            Assert.True((await reopened.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
            Assert.Equal(7, (await reopened.Get<PackageService>().GetCustomerPackagesAsync(customer)).Value.Single().RemainingSessions);
        }

        // 11. Restore from the backup on a "new computer" with the recovery key.
        await using var newPc = await TestApp.CreateAsync(protector: new TestSecretProtector());
        await newPc.SetupBeautyAsync(enableBackup: false);
        var restored = await newPc.Get<BackupService>().RestoreAsync(backupFile, recoveryKey);
        Assert.True(restored.IsSuccess, restored.IsFailure ? restored.Error.Message : null);
        Assert.True((await newPc.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        var pkg = (await newPc.Get<PackageService>().GetCustomerPackagesAsync(customer)).Value.Single();
        Assert.Equal(package, pkg.Id);
        Assert.Equal(7, pkg.RemainingSessions);
        Assert.Equal(Money.FromMajor(12_000m).Minor, (await newPc.Get<PaymentService>().GetBalanceAsync(customer)).Value);
    }
}
