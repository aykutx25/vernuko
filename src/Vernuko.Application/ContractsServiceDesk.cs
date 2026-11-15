using Vernuko.Payments;
using Vernuko.ServiceDesk;

namespace Vernuko.Application;

// ---------- Teknik Servis mode: service tickets ----------

public sealed record TicketIntakeRequest(
    Guid CustomerId,
    DeviceKind Device,
    string Brand,
    string? Model,
    string? SerialNumber,
    string Complaint,
    string? Accessories,
    string? Condition,
    string? BroughtBy,
    bool OnSite,
    string? SiteAddress,
    DateOnly? PromisedDate,
    Guid? TechnicianStaffId);

/// <param name="UnlockCode">Screen code or pattern if the customer gave one; stored encrypted, never printed, erased at delivery.</param>
public sealed record OpenTicketRequest(TicketIntakeRequest Intake, string? UnlockCode, string IdempotencyKey);

public sealed record DeliverTicketRequest(Guid TicketId, string DeliveredTo, long CashMinor, long CardMinor, long TransferMinor, string IdempotencyKey);

public sealed record TicketSummaryDto(
    Guid Id,
    string Number,
    TicketStatus Status,
    string CustomerName,
    string? CustomerPhone,
    DeviceKind Device,
    string DeviceText,
    string? SerialNumber,
    DateTime ReceivedAtUtc,
    DateOnly DueDate,
    string? TechnicianName,
    bool IsOverdue,
    bool AwaitingApproval)
{
    public string StatusText => ServiceRules.StatusName(Status);
}

public sealed record TicketLineDto(Guid Id, int LineNo, TicketLineKind Kind, Guid? ProductId, string Description, int Quantity, long UnitPriceMinor, int VatRate, long TotalMinor, string? StaffName);

public sealed record TicketEventDto(DateTime AtUtc, TicketEventKind Kind, string Text, string? UserName);

/// <summary>An earlier repair of the same serial whose 12-month warranty is still running.</summary>
public sealed record WarrantyCandidateDto(Guid TicketId, string Number, DateOnly DeliveredOn, DateOnly SameFaultUntil, string? WorkDone);

public sealed record TicketDto(
    TicketSummaryDto Summary,
    Guid CustomerId,
    TicketIntakeRequest Intake,
    bool HasUnlockCode,
    string? Diagnosis,
    string? WorkDone,
    long? EstimateMinor,
    long DiagnosticFeeMinor,
    ApprovalState Approval,
    ApprovalChannel? ApprovalChannel,
    DateTime? ApprovalAtUtc,
    string? ApprovalBy,
    string? UnrepairableReason,
    Guid? WarrantyOfTicketId,
    string? WarrantyOfNumber,
    WarrantyCandidateDto? WarrantyCandidate,
    IReadOnlyList<TicketLineDto> Lines,
    long LinesTotalMinor,
    long AmountDueMinor,
    long DepositsMinor,
    DateTime? DeliveredAtUtc,
    string? DeliveredTo,
    bool Repaired,
    long TotalChargedMinor,
    DateOnly? SameFaultWarrantyUntil,
    DateOnly? PartsWarrantyUntil,
    IReadOnlyList<TicketEventDto> History);

public enum SlipKind
{
    /// <summary>Kabul belgesi: given when the device is received (SSHY m.11/1).</summary>
    Intake,

    /// <summary>Servis fişi: given when the device goes back (SSHY m.11/2).</summary>
    Delivery,
}

/// <summary>Everything a printed slip shows; the desktop lays it out. The unlock code is never part of it.</summary>
public sealed record ServiceSlipDto(
    SlipKind Kind,
    string BusinessName,
    string? BusinessAddress,
    string? BusinessPhone,
    string? BusinessTaxText,
    string Number,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerAddress,
    string DeviceText,
    string? SerialNumber,
    string Complaint,
    string? Accessories,
    string? Condition,
    string? BroughtBy,
    DateTime ReceivedAtLocal,
    string? ReceivedBy,
    DateOnly DueDate,
    string? Diagnosis,
    string? WorkDone,
    IReadOnlyList<TicketLineDto> Lines,
    long TotalMinor,
    bool Repaired,
    string? UnrepairableReason,
    DateTime? DeliveredAtLocal,
    string? DeliveredTo,
    DateOnly? SameFaultWarrantyUntil,
    DateOnly? PartsWarrantyUntil,
    IReadOnlyList<string> Notes);
