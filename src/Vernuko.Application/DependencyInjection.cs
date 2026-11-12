using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and all use-case services. Services are stateless singletons that create a short-lived
    /// DbContext per operation (recommended for desktop apps).
    /// </summary>
    public static IServiceCollection AddVernukoApplication(this IServiceCollection services, string databasePath)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new LicensingOptions());
        services.AddDbContextFactory<VernukoDbContext>(o => SqliteSetup.Configure(o, databasePath));
        services.AddSingleton<UserSession>();
        services.AddSingleton<AuditWriter>();
        services.TryAddSingleton(new PasswordHasher());
        // TryAdd: a remote session's container on the main computer shares the main computer's license service.
        services.TryAddSingleton<LicenseService>();
        services.TryAddSingleton<ILicenseGate>(sp => sp.GetRequiredService<LicenseService>());
        services.AddSingleton<LicenseOnlineService>();
        services.TryAddSingleton<ILicenseOnlineService>(sp => sp.GetRequiredService<LicenseOnlineService>());
        services.TryAddSingleton<IMultiComputer, StandaloneComputer>();
        services.AddSingleton<AppServiceDeps>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<BackupKeyStore>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<UserAdminService>();
        services.AddSingleton<CustomerService>();
        services.AddSingleton<CatalogService>();
        services.AddSingleton<AppointmentService>();
        services.AddSingleton<PackageService>();
        services.AddSingleton<PaymentService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<AuditService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<MessagingService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<CloudFolderLocator>();
        services.AddSingleton<AutoMessagingService>();
        services.TryAddSingleton<IMessageTransportFactory>(_ => new HttpMessageTransportFactory(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));
        services.AddSingleton<DemoDataSeeder>();
        services.AddSingleton<CommissionService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<WaitlistService>();
        services.AddSingleton<AttachmentKeyStore>();
        services.AddSingleton<CustomerRecordsService>();

        // Screens depend on these contracts: the real services here, remote proxies on a connected computer.
        services.TryAddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.TryAddSingleton<IUserAdminService>(sp => sp.GetRequiredService<UserAdminService>());
        services.TryAddSingleton<ICustomerService>(sp => sp.GetRequiredService<CustomerService>());
        services.TryAddSingleton<ICatalogService>(sp => sp.GetRequiredService<CatalogService>());
        services.TryAddSingleton<IAppointmentService>(sp => sp.GetRequiredService<AppointmentService>());
        services.TryAddSingleton<IPackageService>(sp => sp.GetRequiredService<PackageService>());
        services.TryAddSingleton<IPaymentService>(sp => sp.GetRequiredService<PaymentService>());
        services.TryAddSingleton<IReportService>(sp => sp.GetRequiredService<ReportService>());
        services.TryAddSingleton<IAuditService>(sp => sp.GetRequiredService<AuditService>());
        services.TryAddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.TryAddSingleton<IMessagingService>(sp => sp.GetRequiredService<MessagingService>());
        services.TryAddSingleton<ICommissionService>(sp => sp.GetRequiredService<CommissionService>());
        services.TryAddSingleton<IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.TryAddSingleton<IWaitlistService>(sp => sp.GetRequiredService<WaitlistService>());
        services.TryAddSingleton<ICustomerRecordsService>(sp => sp.GetRequiredService<CustomerRecordsService>());
        services.TryAddSingleton<IAutoMessagingService>(sp => sp.GetRequiredService<AutoMessagingService>());
        services.TryAddSingleton<IBackupService>(sp => sp.GetRequiredService<BackupService>());
        services.TryAddSingleton<ILicenseService>(sp => sp.GetRequiredService<LicenseService>());
        return services;
    }
}
