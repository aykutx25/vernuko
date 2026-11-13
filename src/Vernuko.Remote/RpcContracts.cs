using System.Reflection;
using Vernuko.Application.Services;
using Vernuko.Core;

namespace Vernuko.Remote;

/// <summary>The contracts a connected computer may call, and the calls that stay on the main computer.</summary>
public static class RpcContracts
{
    public static readonly IReadOnlyList<Type> All =
    [
        typeof(IAuthService), typeof(IUserAdminService), typeof(ICustomerService), typeof(ICatalogService), typeof(IAppointmentService),
        typeof(IPackageService), typeof(IPaymentService), typeof(IReportService), typeof(IAuditService), typeof(ISettingsService),
        typeof(IMessagingService), typeof(ICommissionService), typeof(IInventoryService), typeof(IWaitlistService),
        typeof(ICustomerRecordsService), typeof(IPrivateNotesService), typeof(IAutoMessagingService), typeof(IBackupService), typeof(ILicenseService),
    ];

    /// <summary>
    /// Work with paths on the main computer's disk, background jobs and license-file handling: only on the main
    /// computer itself. (Backups are still taken there on request; restoring needs someone at that computer.)
    /// </summary>
    public static readonly IReadOnlySet<string> HostOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "IBackupService/ConfigureAsync", "IBackupService/VerifyAsync", "IBackupService/RestoreAsync", "IBackupService/RestoreIntoNewInstallationAsync",
        "IBackupService/EnableCloudCopyAsync", "IBackupService/DisableCloudCopyAsync", "IBackupService/CreateRecoveryKeyAsync", "IBackupService/ConfirmRecoveryKeyAsync",
        "IBackupService/IsScheduledBackupDueAsync",
        "ILicenseService/ImportAsync", "ILicenseService/StoreFromServerAsync", "ILicenseService/RemoveLocalAsync", "ILicenseService/AdoptServerTrialStartAsync",
        "IAutoMessagingService/PlanRemindersAsync", "IAutoMessagingService/DispatchDueAsync",
    };

    /// <summary>Allowed for a paired computer before anyone signs in (the login screen shows the business name).</summary>
    public static readonly IReadOnlySet<string> BeforeLogin = new HashSet<string>(StringComparer.Ordinal)
    {
        "ISettingsService/GetBusinessInfoAsync",
    };

    public static readonly Error HostOnlyError = new("remote.host_only", "Bu işlem yalnızca ana bilgisayarda yapılabilir.");

    public static string Key(Type contract, MethodInfo method) => contract.Name + "/" + method.Name;

    /// <summary>Every remotable method by "IContract/Method". Overloads are not allowed (one name, one method).</summary>
    public static IReadOnlyDictionary<string, (Type Contract, MethodInfo Method)> Methods { get; } = BuildMethods();

    private static Dictionary<string, (Type, MethodInfo)> BuildMethods()
    {
        var map = new Dictionary<string, (Type, MethodInfo)>(StringComparer.Ordinal);
        foreach (var contract in All)
        {
            foreach (var m in contract.GetMethods().Where(m => typeof(Task).IsAssignableFrom(m.ReturnType)))
            {
                if (!map.TryAdd(Key(contract, m), (contract, m)))
                {
                    throw new InvalidOperationException($"Overloaded remote method {Key(contract, m)} is not supported.");
                }
            }
        }

        return map;
    }
}
