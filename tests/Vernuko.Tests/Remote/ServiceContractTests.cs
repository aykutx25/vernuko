using System.Reflection;
using Vernuko.Application.Services;

namespace Vernuko.Tests.Remote;

public class ServiceContractTests
{
    public static TheoryData<Type> Services() =>
    [
        typeof(AuthService), typeof(UserAdminService), typeof(CustomerService), typeof(CatalogService), typeof(AppointmentService),
        typeof(PackageService), typeof(PaymentService), typeof(ReportService), typeof(AuditService), typeof(SettingsService),
        typeof(MessagingService), typeof(CommissionService), typeof(InventoryService), typeof(WaitlistService),
        typeof(CustomerRecordsService), typeof(AutoMessagingService), typeof(BackupService), typeof(LicenseService),
    ];

    /// <summary>
    /// Screens only see the interface; a public async method missing from it would silently be unavailable on a
    /// connected computer. Regenerate with tools/ServiceContractGenerator when this fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(Services))]
    public void Every_public_async_service_method_is_in_its_contract(Type service)
    {
        var contract = service.GetInterface("I" + service.Name);
        Assert.NotNull(contract);
        var implemented = service.GetInterfaceMap(contract).TargetMethods.ToHashSet();
        var missing = service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && typeof(Task).IsAssignableFrom(m.ReturnType) && !implemented.Contains(m))
            .Select(m => m.Name)
            .ToList();
        Assert.Empty(missing);
    }
}
