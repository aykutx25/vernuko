using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Remote;
using Vernuko.Security;
using Vernuko.ServiceDesk;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Regression tests for the second security review (0.16.1).</summary>
public class SecurityRound2Tests
{
    [Fact]
    public async Task A_changed_or_deactivated_account_loses_its_open_session_but_own_password_change_keeps_it()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        Assert.True((await app.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", SystemRole.Secretary)).IsSuccess);
        var auth = app.Get<AuthService>();
        Assert.True((await auth.LoginAsync("sekreter", "Randevu-Masasi-1")).IsSuccess);
        var customers = app.Get<CustomerService>();
        Assert.True((await customers.SearchAsync(null)).IsSuccess);

        // Own password change: this session stays.
        Assert.True((await auth.ChangeOwnPasswordAsync("Randevu-Masasi-1", "Randevu-Masasi-2")).IsSuccess);
        Assert.True((await customers.SearchAsync(null)).IsSuccess);

        // Deactivated by the owner (here: directly in the database, as from another computer): the next call ends it.
        await using (var db = await app.DbAsync())
        {
            var user = await db.Users.SingleAsync(u => u.Username == "sekreter");
            user.Deactivate();
            await db.SaveChangesAsync();
        }

        Assert.Equal("auth.session_revoked", (await customers.SearchAsync(null)).Error.Code);
        Assert.False(app.Get<UserSession>().IsAuthenticated);
    }

    [Fact]
    public async Task Changing_the_password_with_a_wrong_current_one_counts_toward_the_lock_out()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var auth = app.Get<AuthService>();
        Result last = Result.Success();
        for (var i = 0; i < 10 && app.Get<UserSession>().IsAuthenticated; i++)
        {
            last = await auth.ChangeOwnPasswordAsync("yanlis-parola", "Yeni-Parola-2026");
        }

        Assert.Equal("auth.locked", last.Error.Code);
        Assert.False(app.Get<UserSession>().IsAuthenticated);
    }

    [Fact]
    public void A_connected_computer_may_ask_only_for_a_manual_backup_and_needs_settings_rights_for_the_license_document()
    {
        var session = new UserSession();
        Assert.Null(RpcContracts.RemoteRefusal("IBackupService/CreateBackupAsync", [BackupTrigger.Manual, default(CancellationToken)], session));
        Assert.NotNull(RpcContracts.RemoteRefusal("IBackupService/CreateBackupAsync", [BackupTrigger.PreMigration, default(CancellationToken)], session));
        Assert.NotNull(RpcContracts.RemoteRefusal("IBackupService/CreateBackupAsync", [BackupTrigger.PreUpdate, default(CancellationToken)], session));
        Assert.NotNull(RpcContracts.RemoteRefusal("ILicenseService/GetDocumentAsync", [default(CancellationToken)], session));
        session.SignInSystem("test", new HashSet<Permission> { Permission.SettingsManage });
        Assert.Null(RpcContracts.RemoteRefusal("ILicenseService/GetDocumentAsync", [default(CancellationToken)], session));
    }

    [Fact]
    public async Task Costs_supplier_money_and_workshop_payments_need_their_own_rights()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.PhoneRepair);
        var commerce = app.Get<CommerceService>();
        var supplier = (await commerce.SaveSupplierAsync(new SupplierDto(Guid.Empty, "Parça Toptan", null, null, null, null, null, null, null, true, 0))).Value;
        var customer = await app.AddCustomerAsync();
        var desk = app.Get<ServiceDeskService>();
        var ticket = (await desk.OpenTicketAsync(new OpenTicketRequest(new TicketIntakeRequest(customer, DeviceKind.Phone, "Telsa", null, null, "Açılmıyor", null, null, null, false, null, null, null), null, "t-1"))).Value;

        // A technician (Specialist): works on tickets, does not take money or see costs and supplier balances.
        Assert.True((await app.Get<UserAdminService>().CreateAsync("teknisyen", "Teknisyen", "Servis-Tezgahi-1", SystemRole.Specialist)).IsSuccess);
        Assert.True((await app.Get<AuthService>().LoginAsync("teknisyen", "Servis-Tezgahi-1")).IsSuccess);

        Assert.Equal(Error.Forbidden.Code, (await desk.TakeDepositAsync(ticket.Summary.Id, 1_000, PaymentMethod.Cash, "k-1")).Error.Code);
        Assert.True((await desk.SetDiagnosisAsync(ticket.Summary.Id, "Anakart", null)).IsSuccess);
        if ((await commerce.GetSuppliersAsync()).IsSuccess)
        {
            Assert.All((await commerce.GetSuppliersAsync()).Value, s => Assert.Equal(0, s.BalanceMinor));
        }

        Assert.True((await commerce.GetSupplierStatementAsync(supplier)).IsFailure);
    }

    [Fact]
    public async Task Erasing_a_customer_also_clears_names_on_service_tickets_and_the_export_lists_them()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.PhoneRepair);
        var customer = await app.AddCustomerAsync();
        var desk = app.Get<ServiceDeskService>();
        var t = (await desk.OpenTicketAsync(new OpenTicketRequest(new TicketIntakeRequest(customer, DeviceKind.Phone, "Telsa", "T12", "352099001761481", "Ekran kırık", null, null, "Mehmet Getiren", true, "Moda Cad. 5", null, null), "1234", "t-1"))).Value;

        var export = (await app.Get<CustomerService>().ExportCustomerJsonAsync(customer)).Value;
        Assert.Contains("SF-2026-000001", export, StringComparison.Ordinal);
        Assert.Contains("Mehmet Getiren", export, StringComparison.Ordinal);

        Assert.True((await app.Get<CustomerService>().AnonymizeAsync(customer, "Talep")).IsSuccess);
        var after = (await desk.GetTicketAsync(t.Summary.Id)).Value;
        Assert.Null(after.Intake.BroughtBy);
        Assert.NotEqual("Moda Cad. 5", after.Intake.SiteAddress);
        Assert.False(after.HasUnlockCode);
        Assert.Equal("Ekran kırık", after.Intake.Complaint); // the service record stays
    }
}
