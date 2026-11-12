namespace Vernuko.Security;

/// <summary>
/// Operation-level permissions. Stored by name (not by number) so the list can grow safely.
/// </summary>
public enum Permission
{
    AppointmentsView,
    AppointmentsManage,
    AppointmentsChangeStatus,
    CustomersView,
    CustomersManage,
    CustomersExport,
    CustomersArchiveOrAnonymize,
    CustomerSensitiveDataView,
    PrivateNotesViewOwn,
    CatalogManage,
    StaffManage,
    PackagesView,
    PackagesSell,
    PackagesAdjustSessions,
    PaymentsView,
    PaymentsRecord,
    PaymentsReverse,
    ReportsOperational,
    ReportsFinancial,
    SettingsManage,
    UsersManage,
    BackupsCreate,
    BackupsManage,
    BackupsRestore,
    AuditLogView,
    LicenseManage,
    MessagingSend,
    IntegrationsConfigure,
    ConsentsRecord,
    CustomerFilesView,
    CustomerFilesManage,
    InventoryView,
    InventoryManage,
    ProductsSell,
}

public enum SystemRole
{
    Owner,
    Manager,
    Secretary,
    Specialist,
    Staff,
    Accounting,
    Custom,
}

public static class RoleDefaults
{
    private static readonly Permission[] All = Enum.GetValues<Permission>();

    public static IReadOnlySet<Permission> For(SystemRole role) => role switch
    {
        SystemRole.Owner => All.ToHashSet(),
        SystemRole.Manager => All.Except([Permission.LicenseManage, Permission.BackupsRestore]).ToHashSet(),
        SystemRole.Secretary => new HashSet<Permission>
        {
            Permission.AppointmentsView, Permission.AppointmentsManage, Permission.AppointmentsChangeStatus,
            Permission.CustomersView, Permission.CustomersManage,
            Permission.PackagesView, Permission.PackagesSell,
            Permission.PaymentsView, Permission.PaymentsRecord,
            Permission.ReportsOperational, Permission.BackupsCreate, Permission.MessagingSend,
            Permission.ConsentsRecord, Permission.InventoryView, Permission.ProductsSell,
        },
        SystemRole.Specialist => new HashSet<Permission>
        {
            Permission.AppointmentsView, Permission.AppointmentsChangeStatus,
            Permission.CustomersView, Permission.PackagesView, Permission.PrivateNotesViewOwn,
            Permission.ConsentsRecord, Permission.CustomerFilesView, Permission.CustomerFilesManage, Permission.InventoryView,
        },
        SystemRole.Staff => new HashSet<Permission>
        {
            Permission.AppointmentsView, Permission.AppointmentsChangeStatus, Permission.CustomersView,
        },
        SystemRole.Accounting => new HashSet<Permission>
        {
            Permission.AppointmentsView, Permission.CustomersView, Permission.PackagesView,
            Permission.PaymentsView, Permission.PaymentsRecord, Permission.PaymentsReverse,
            Permission.ReportsOperational, Permission.ReportsFinancial, Permission.InventoryView,
        },
        SystemRole.Custom => new HashSet<Permission>(),
        _ => new HashSet<Permission>(),
    };

    public static string DisplayName(SystemRole role) => role switch
    {
        SystemRole.Owner => "İşletme sahibi",
        SystemRole.Manager => "Yönetici",
        SystemRole.Secretary => "Sekreter",
        SystemRole.Specialist => "Uzman",
        SystemRole.Staff => "Personel",
        SystemRole.Accounting => "Muhasebe",
        SystemRole.Custom => "Özel rol",
        _ => role.ToString(),
    };
}
