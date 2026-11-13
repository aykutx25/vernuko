// Generated from the compiled services. Regenerate with the interface generator (docs/MULTI_PC.md) when a service changes;
// ServiceContractTests fails if a public async service method is missing here.
using Vernuko.Application;
using Vernuko.Appointments;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Licensing;
using Vernuko.Messaging;
using Vernuko.Reporting;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Remotable contract of <see cref="AuthService"/> (generated from its public async methods).</summary>
public partial interface IAuthService
{
    Task<Result> LoginAsync(string username, string password, CancellationToken ct = default);

    Task<Result> UnlockAsync(string password, CancellationToken ct = default);

    Task<Result> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="UserAdminService"/> (generated from its public async methods).</summary>
public partial interface IUserAdminService
{
    Task<Result<IReadOnlyList<UserSummary>>> ListAsync(CancellationToken ct = default);

    Task<Result<Guid>> CreateAsync(string username, string displayName, string password, SystemRole role, CancellationToken ct = default);

    Task<Result<bool>> UpdateAsync(Guid userId, string displayName, SystemRole role, bool isActive, CancellationToken ct = default);

    Task<Result<bool>> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="CustomerService"/> (generated from its public async methods).</summary>
public partial interface ICustomerService
{
    Task<Result<IReadOnlyList<CustomerListItem>>> SearchAsync(string? query, bool includeArchived = false, int take = 50, CancellationToken ct = default);

    Task<Result<CustomerDetailsDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DuplicateCandidate>>> FindDuplicatesAsync(string firstName, string lastName, string? phone, Guid? excludeId = null, CancellationToken ct = default);

    Task<Result<Guid>> CreateAsync(CustomerDetails details, bool allowDuplicates, CancellationToken ct = default);

    Task<Result<bool>> UpdateAsync(Guid id, long expectedVersion, CustomerDetails details, bool allowSms, bool allowWhatsApp, bool allowEmail, bool marketingConsent, CancellationToken ct = default);

    Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default);

    Task<Result<bool>> RestoreAsync(Guid id, CancellationToken ct = default);

    Task<Result<bool>> AnonymizeAsync(Guid id, string reason, CancellationToken ct = default);

    Task<Result<string>> ExportCustomerJsonAsync(Guid id, CancellationToken ct = default);

    Task<Result<string>> ExportAllCsvAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<AppointmentListItem>>> GetHistoryAsync(Guid customerId, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="CatalogService"/> (generated from its public async methods).</summary>
public partial interface ICatalogService
{
    Task<Result<IReadOnlyList<ServiceDto>>> GetServicesAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SaveServiceAsync(ServiceDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StaffDto>>> GetStaffAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SaveStaffAsync(StaffDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ResourceDto>>> GetResourcesAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SaveResourceAsync(ResourceDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ScheduleExceptionDto>>> GetExceptionsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<Result<Guid>> AddExceptionAsync(ScheduleExceptionScope scope, Guid? targetId, ScheduleExceptionKind kind, DateTime startLocal, DateTime endLocal, string? note, CancellationToken ct = default);

    Task<Result<bool>> RemoveExceptionAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PackageTemplateDto>>> GetPackageTemplatesAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SavePackageTemplateAsync(PackageTemplateDto dto, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="AppointmentService"/> (generated from its public async methods).</summary>
public partial interface IAppointmentService
{
    Task<Result<DayAgenda>> GetDayAsync(DateOnly day, AppointmentFilter filter, CancellationToken ct = default);

    Task<Result<IReadOnlyList<AppointmentListItem>>> GetRangeAsync(DateOnly from, DateOnly toInclusive, AppointmentFilter filter, CancellationToken ct = default);

    Task<Result<IReadOnlyList<AppointmentListItem>>> GetUpcomingAsync(int take, CancellationToken ct = default);

    Task<Result<AppointmentListItem>> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<SlotSuggestion>>> GetAvailableSlotsAsync(SlotQuery q, CancellationToken ct = default);

    Task<Result<BookingOutcome>> CreateAsync(CreateAppointmentRequest r, bool confirmWarnings, CancellationToken ct = default);

    Task<Result<BookingOutcome>> RescheduleAsync(RescheduleRequest r, bool confirmWarnings, CancellationToken ct = default);

    Task<Result<SeriesOutcome>> CreateSeriesAsync(CreateAppointmentRequest r, RecurrencePattern pattern, bool confirmWarnings, bool skipBlocked, CancellationToken ct = default);

    Task<Result<int>> CancelSeriesFromAsync(Guid appointmentId, string reason, CancellationToken ct = default);

    Task<Result<AppointmentStatus>> ChangeStatusAsync(Guid id, AppointmentStatus to, string? reason, CancellationToken ct = default);

    Task<Result<bool>> UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default);

    Task<Result<bool>> SetMeetingUrlAsync(Guid id, string? url, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="PackageService"/> (generated from its public async methods).</summary>
public partial interface IPackageService
{
    Task<Result<Guid>> SellAsync(SellPackageRequest r, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CustomerPackageDto>>> GetCustomerPackagesAsync(Guid customerId, bool activeOnly = false, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CustomerPackageDto>>> SearchAsync(string? customerQuery, bool activeOnly, CancellationToken ct = default);

    Task<Result<IReadOnlyList<SessionLedgerDto>>> GetLedgerAsync(Guid packageId, CancellationToken ct = default);

    Task<Result<int>> AdjustSessionsAsync(Guid packageId, int delta, string reason, CancellationToken ct = default);

    Task<Result<bool>> FreezeAsync(Guid packageId, bool freeze, CancellationToken ct = default);

    Task<Result<bool>> ExtendAsync(Guid packageId, int days, CancellationToken ct = default);

    Task<Result<bool>> CancelAsync(Guid packageId, string reason, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="PaymentService"/> (generated from its public async methods).</summary>
public partial interface IPaymentService
{
    Task<Result<Guid>> RecordAsync(RecordTransactionRequest r, CancellationToken ct = default);

    Task<Result<Guid>> ReverseAsync(Guid transactionId, string reason, CancellationToken ct = default);

    Task<Result<long>> GetBalanceAsync(Guid customerId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<TransactionDto>>> GetCustomerTransactionsAsync(Guid customerId, CancellationToken ct = default);

    Task<Result<CashDaySummary>> GetCashDayAsync(DateOnly day, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DebtorDto>>> GetDebtorsAsync(CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="ReportService"/> (generated from its public async methods).</summary>
public partial interface IReportService
{
    Task<Result<DailySummary>> GetDailySummaryAsync(DateOnly day, CancellationToken ct = default);

    Task<Result<PeriodReport>> GetPeriodReportAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="AuditService"/> (generated from its public async methods).</summary>
public partial interface IAuditService
{
    Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(int take, CancellationToken ct = default);

    Task<Result<AuditVerification>> VerifyChainAsync(CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="SettingsService"/> (generated from its public async methods).</summary>
public partial interface ISettingsService
{
    Task<BusinessInfo?> GetBusinessInfoAsync(CancellationToken ct = default);

    Task<Result<bool>> UpdateBusinessAsync(string name, string? phone, string? email, string? address, WeeklySchedule hours, int slotMinutes, bool noShowConsumesSession, bool autoConfirm, int autoLockMinutes, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="MessagingService"/> (generated from its public async methods).</summary>
public partial interface IMessagingService
{
    Task<Result<ManualMessage>> PrepareWhatsAppAsync(Guid appointmentId, MessagePurpose purpose, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="CommissionService"/> (generated from its public async methods).</summary>
public partial interface ICommissionService
{
    Task<Result<IReadOnlyList<CommissionRuleDto>>> GetRulesAsync(CancellationToken ct = default);

    Task<Result<Guid>> SaveRuleAsync(CommissionRuleDto dto, CancellationToken ct = default);

    Task<Result<CommissionReport>> GetReportAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="InventoryService"/> (generated from its public async methods).</summary>
public partial interface IInventoryService
{
    Task<Result<IReadOnlyList<ProductDto>>> GetProductsAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SaveProductAsync(ProductDto dto, CancellationToken ct = default);

    Task<Result<int>> ReceiveAsync(Guid productId, int quantity, long? unitCostMinor, string? note, CancellationToken ct = default);

    Task<Result<int>> AdjustAsync(Guid productId, int delta, string reason, CancellationToken ct = default);

    Task<Result<Guid>> SellAsync(SellProductRequest r, CancellationToken ct = default);

    Task<Result<IReadOnlyList<StockMovementDto>>> GetMovementsAsync(Guid productId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ServiceMaterialDto>>> GetServiceMaterialsAsync(Guid serviceId, CancellationToken ct = default);

    Task<Result<bool>> SetServiceMaterialsAsync(Guid serviceId, IReadOnlyList<(Guid ProductId, int Quantity)> materials, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="WaitlistService"/> (generated from its public async methods).</summary>
public partial interface IWaitlistService
{
    Task<Result<IReadOnlyList<WaitlistItem>>> ListAsync(bool includeClosed = false, CancellationToken ct = default);

    Task<Result<Guid>> AddAsync(Guid customerId, Guid? serviceId, Guid? staffMemberId, DateOnly from, DateOnly to, string? preferredTime, string? notes, CancellationToken ct = default);

    Task<Result<bool>> SetStatusAsync(Guid id, WaitlistStatus status, Guid? appointmentId = null, CancellationToken ct = default);

    Task<Result<IReadOnlyList<WaitlistItem>>> FindMatchesForAsync(Guid appointmentId, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="CustomerRecordsService"/> (generated from its public async methods).</summary>
public partial interface ICustomerRecordsService
{
    Task<Result<IReadOnlyList<ConsentTemplateDto>>> GetTemplatesAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<Result<Guid>> SaveTemplateAsync(ConsentTemplateDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CustomerConsentDto>>> GetConsentsAsync(Guid customerId, CancellationToken ct = default);

    Task<Result<Guid>> RecordConsentAsync(Guid customerId, Guid templateId, ConsentMethod method, string? note, Guid? scanFileId = null, CancellationToken ct = default);

    Task<Result<bool>> RevokeConsentAsync(Guid consentId, string reason, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CustomerFileDto>>> GetFilesAsync(Guid customerId, CancellationToken ct = default);

    Task<Result<Guid>> AddFileAsync(Guid customerId, CustomerFileKind kind, string originalName, byte[] content, string? note, CancellationToken ct = default);

    Task<Result<byte[]>> ReadFileAsync(Guid fileId, CancellationToken ct = default);

    Task<Result<bool>> DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="PrivateNotesService"/> (generated from its public async methods).</summary>
public partial interface IPrivateNotesService
{
    Task<Result<PrivateNotesView>> GetAsync(Guid customerId, CancellationToken ct = default);

    Task<Result<Guid>> AddAsync(Guid customerId, string text, CancellationToken ct = default);

    Task<Result<bool>> DeleteAsync(Guid noteId, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="AutoMessagingService"/> (generated from its public async methods).</summary>
public partial interface IAutoMessagingService
{
    Task<Result<MessagingSettings>> GetSettingsAsync(CancellationToken ct = default);

    Task<Result<bool>> SaveSettingsAsync(MessagingSettingsUpdate u, CancellationToken ct = default);

    Task<Result<string>> SendTestAsync(MessageChannel channel, string phone, CancellationToken ct = default);

    Task<Result<IReadOnlyList<OutboxItem>>> GetOutboxAsync(int take = 100, CancellationToken ct = default);

    Task<Result<MessagingMonth>> GetMonthAsync(CancellationToken ct = default);

    Task<int> PlanRemindersAsync(CancellationToken ct = default);

    Task<int> DispatchDueAsync(CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="BackupService"/> (generated from its public async methods).</summary>
public partial interface IBackupService
{
    Task<BackupStatus> GetStatusAsync(CancellationToken ct = default);

    Task<Result<bool>> EnableCloudCopyAsync(string syncFolder, CloudProvider provider, bool consentGiven, CancellationToken ct = default);

    Task<Result<bool>> DisableCloudCopyAsync(CancellationToken ct = default);

    Task<Result<bool>> ConfigureAsync(string folder, BackupSchedule schedule, bool onExit, RetentionPolicy retention, CancellationToken ct = default);

    Task<Result<string>> CreateRecoveryKeyAsync(CancellationToken ct = default);

    Task<Result<bool>> ConfirmRecoveryKeyAsync(string typedKey, CancellationToken ct = default);

    Task<bool> IsScheduledBackupDueAsync(CancellationToken ct = default);

    Task<Result<BackupRunResult>> CreateBackupAsync(BackupTrigger trigger, CancellationToken ct = default);

    Task<IReadOnlyList<BackupListItem>> ListBackupsAsync(CancellationToken ct = default);

    Task<Result<BackupVerifyResult>> VerifyAsync(string backupPath, string? recoveryKey, CancellationToken ct = default);

    Task<Result<RestoreResult>> RestoreAsync(string backupPath, string? recoveryKey, CancellationToken ct = default);

    Task<Result<RestoreResult>> RestoreIntoNewInstallationAsync(string backupPath, string recoveryKey, CancellationToken ct = default);
}

/// <summary>Remotable contract of <see cref="LicenseService"/> (generated from its public async methods).</summary>
public partial interface ILicenseService
{
    Task<LicenseStatus> RefreshAsync(CancellationToken ct = default);

    Task<Result<LicenseStatus>> ImportAsync(string signedLicenseJson, CancellationToken ct = default);

    Task<Result<LicenseStatus>> StoreFromServerAsync(string signedLicenseJson, bool activation, CancellationToken ct = default);

    Task<LicenseDocument?> GetDocumentAsync(CancellationToken ct = default);

    Task<LicenseStatus> RemoveLocalAsync(string reason, CancellationToken ct = default);

    Task<LicenseStatus> AdoptServerTrialStartAsync(DateTime serverStartUtc, CancellationToken ct = default);
}
