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
        services.AddSingleton<LicenseService>();
        services.AddSingleton<ILicenseGate>(sp => sp.GetRequiredService<LicenseService>());
        services.AddSingleton<LicenseOnlineService>();
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
        return services;
    }
}
