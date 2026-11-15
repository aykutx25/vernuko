using Vernuko.Core;

namespace Vernuko.ServiceDesk;

public enum DeviceKind
{
    Phone,
    Tablet,
    Laptop,
    Desktop,
    Television,
    WhiteGoods,
    AirConditioner,
    SmallAppliance,
    Other,
}

/// <summary>Where a device is. Delivered is final; every change is written to the ticket's history.</summary>
public enum TicketStatus
{
    /// <summary>Teslim alındı.</summary>
    Received,

    /// <summary>Arıza tespitinde.</summary>
    Diagnosing,

    /// <summary>Fiyat onayı bekliyor.</summary>
    AwaitingApproval,

    /// <summary>Parça bekliyor.</summary>
    AwaitingParts,

    /// <summary>Onarımda.</summary>
    Repairing,

    /// <summary>Hazır, teslim bekliyor.</summary>
    Ready,

    /// <summary>Onarılamadı / onarım istenmedi: geri verilecek.</summary>
    Unrepairable,

    /// <summary>Müşteriye teslim edildi.</summary>
    Delivered,
}

public enum ApprovalState
{
    /// <summary>No price asked (e.g. a fixed-price job agreed at the counter).</summary>
    NotAsked,
    Pending,
    Approved,
    Declined,
}

public enum ApprovalChannel
{
    InPerson,
    Phone,
    Sms,
    WhatsApp,
    SignedForm,
}

public enum TicketLineKind
{
    Part,
    Labor,
}

public enum TicketEventKind
{
    Created,
    StatusChanged,
    EstimateSet,
    ApprovalRecorded,
    LineAdded,
    LineRemoved,
    Delivered,
    UnlockCodeViewed,
    Note,
    MessageQueued,
}

/// <summary>
/// The legal frame of a service ticket (6502 sayılı Kanun, Satış Sonrası Hizmetler Yönetmeliği), for an independent
/// ("özel servis") repair shop. Shown on the slips; the shop's lawyer has the final word.
/// </summary>
public static class ServiceRules
{
    /// <summary>The same fault within this time after an out-of-warranty repair is repaired free (SSHY m.10/6).</summary>
    public const int SameFaultWarrantyMonths = 12;

    /// <summary>Parts the shop replaces carry at least this warranty (SSHY m.10/7).</summary>
    public const int PartsWarrantyMonths = 6;

    /// <summary>Default promise when none is given at the counter: a warning, not a rule for özel servis.</summary>
    public const int DefaultRepairBusinessDays = 20;

    /// <summary>Records must be kept this long (SSHY m.16).</summary>
    public const int RetentionYears = 3;

    public static DateOnly AddBusinessDays(DateOnly start, int days)
    {
        var d = start;
        var added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }

        return d;
    }

    public static string StatusName(TicketStatus s) => s switch
    {
        TicketStatus.Received => "Teslim alındı",
        TicketStatus.Diagnosing => "Arıza tespitinde",
        TicketStatus.AwaitingApproval => "Fiyat onayı bekliyor",
        TicketStatus.AwaitingParts => "Parça bekliyor",
        TicketStatus.Repairing => "Onarımda",
        TicketStatus.Ready => "Hazır",
        TicketStatus.Unrepairable => "Onarılmadı, iade edilecek",
        _ => "Teslim edildi",
    };

    public static string DeviceName(DeviceKind k) => k switch
    {
        DeviceKind.Phone => "Telefon",
        DeviceKind.Tablet => "Tablet",
        DeviceKind.Laptop => "Dizüstü bilgisayar",
        DeviceKind.Desktop => "Masaüstü bilgisayar",
        DeviceKind.Television => "Televizyon",
        DeviceKind.WhiteGoods => "Beyaz eşya",
        DeviceKind.AirConditioner => "Klima",
        DeviceKind.SmallAppliance => "Küçük ev aleti",
        _ => "Diğer",
    };

    public static string ApprovalChannelName(ApprovalChannel c) => c switch
    {
        ApprovalChannel.InPerson => "Yüz yüze",
        ApprovalChannel.Phone => "Telefonla",
        ApprovalChannel.Sms => "SMS ile",
        ApprovalChannel.WhatsApp => "WhatsApp ile",
        _ => "İmzalı form",
    };

    /// <summary>
    /// Serial/IMEI as letters and digits only, upper case: "35-209900-176148-1" and "35 209900 176148 1" are the same
    /// device, so the device history finds it however it was typed.
    /// </summary>
    public static string? NormalizeSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        var s = new string(serial.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return s.Length == 0 ? null : s;
    }
}

public sealed record IntakeDetails(
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

/// <summary>Servis fişi: one device brought for repair, from intake to delivery.</summary>
public sealed class ServiceTicket : Entity
{
    private readonly List<ServiceTicketLine> _lines = [];

    private ServiceTicket()
    {
    }

    public string Number { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public DeviceKind Device { get; private set; }

    public string Brand { get; private set; } = string.Empty;

    public string? Model { get; private set; }

    /// <summary>Serial number or IMEI, normalised.</summary>
    public string? SerialNumber { get; private set; }

    /// <summary>The customer's complaint and request, in their words.</summary>
    public string Complaint { get; private set; } = string.Empty;

    public string? Accessories { get; private set; }

    /// <summary>Physical condition at intake (scratches, cracks …).</summary>
    public string? Condition { get; private set; }

    /// <summary>Who handed the device over (SSHY m.11/1).</summary>
    public string? BroughtBy { get; private set; }

    public bool OnSite { get; private set; }

    public string? SiteAddress { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    public Guid? ReceivedByUserId { get; private set; }

    public Guid? TechnicianStaffId { get; private set; }

    /// <summary>The date promised at the counter (by default 20 iş günü after intake).</summary>
    public DateOnly DueDate { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime StatusChangedAtUtc { get; private set; }

    public string? Diagnosis { get; private set; }

    public string? WorkDone { get; private set; }

    public long? EstimateMinor { get; private set; }

    public ApprovalState Approval { get; private set; }

    public ApprovalChannel? ApprovalChannel { get; private set; }

    public DateTime? ApprovalAtUtc { get; private set; }

    public string? ApprovalBy { get; private set; }

    public string? UnrepairableReason { get; private set; }

    /// <summary>A diagnostic fee agreed in advance, charged when the device goes back unrepaired.</summary>
    public long DiagnosticFeeMinor { get; private set; }

    /// <summary>An earlier ticket of the same device whose repair warranty covers this one (no labour charged).</summary>
    public Guid? WarrantyOfTicketId { get; private set; }

    /// <summary>Unlock code/pattern, encrypted by the application; never printed; erased at delivery.</summary>
    public byte[]? UnlockCodeProtected { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public string? DeliveredTo { get; private set; }

    public bool Repaired { get; private set; }

    public long TotalChargedMinor { get; private set; }

    public IReadOnlyList<ServiceTicketLine> Lines => _lines;

    public bool IsOpen => Status != TicketStatus.Delivered;

    public static Result<ServiceTicket> Open(string number, IntakeDetails d, Guid? userId, DateTime nowUtc, DateOnly today, byte[]? unlockCodeProtected)
    {
        var t = new ServiceTicket
        {
            Number = Guard.NotBlank(number),
            CustomerId = d.CustomerId,
            ReceivedAtUtc = Guard.Utc(nowUtc),
            ReceivedByUserId = userId,
            Status = TicketStatus.Received,
            StatusChangedAtUtc = nowUtc,
            Approval = ApprovalState.NotAsked,
            UnlockCodeProtected = unlockCodeProtected,
        };
        var r = t.UpdateIntake(d, today);
        return r.IsFailure ? r.Error : t;
    }

    public Result UpdateIntake(IntakeDetails d, DateOnly today)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (string.IsNullOrWhiteSpace(d.Brand) || d.Brand.Trim().Length > 60)
        {
            return Error.Validation("ticket.brand", "Marka zorunludur (en fazla 60 karakter).");
        }

        if (string.IsNullOrWhiteSpace(d.Complaint))
        {
            return Error.Validation("ticket.complaint", "Müşterinin şikayetini veya isteğini yazın; servis fişinde yer alır.");
        }

        if (d.OnSite && string.IsNullOrWhiteSpace(d.SiteAddress))
        {
            return Error.Validation("ticket.address", "Adreste serviste adres zorunludur.");
        }

        if (d.PromisedDate is { } p && p < DateOnly.FromDateTime(ReceivedAtUtc).AddDays(-1))
        {
            return Error.Validation("ticket.due", "Teslim tarihi kabul tarihinden önce olamaz.");
        }

        CustomerId = d.CustomerId;
        Device = d.Device;
        Brand = d.Brand.Trim();
        Model = Clean(d.Model);
        SerialNumber = ServiceRules.NormalizeSerial(d.SerialNumber);
        Complaint = d.Complaint.Trim();
        Accessories = Clean(d.Accessories);
        Condition = Clean(d.Condition);
        BroughtBy = Clean(d.BroughtBy);
        OnSite = d.OnSite;
        SiteAddress = d.OnSite ? Clean(d.SiteAddress) : null;
        TechnicianStaffId = d.TechnicianStaffId;
        DueDate = d.PromisedDate ?? ServiceRules.AddBusinessDays(today, ServiceRules.DefaultRepairBusinessDays);
        return Result.Success();
    }

    public Result SetDiagnosis(string? diagnosis, string? workDone)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        Diagnosis = Clean(diagnosis);
        WorkDone = Clean(workDone);
        return Result.Success();
    }

    /// <summary>A price is proposed: the device waits for the customer's answer.</summary>
    public Result SetEstimate(long amountMinor, long diagnosticFeeMinor, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (amountMinor < 0 || diagnosticFeeMinor < 0)
        {
            return Error.Validation("ticket.estimate", "Tutar negatif olamaz.");
        }

        EstimateMinor = amountMinor;
        DiagnosticFeeMinor = diagnosticFeeMinor;
        Approval = ApprovalState.Pending;
        ApprovalChannel = null;
        ApprovalAtUtc = null;
        ApprovalBy = null;
        Move(TicketStatus.AwaitingApproval, nowUtc);
        return Result.Success();
    }

    public Result RecordApproval(bool approved, ApprovalChannel channel, string? by, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (Approval != ApprovalState.Pending)
        {
            return Error.Validation("ticket.no_estimate", "Önce bir fiyat teklifi girin.");
        }

        Approval = approved ? ApprovalState.Approved : ApprovalState.Declined;
        ApprovalChannel = channel;
        ApprovalAtUtc = Guard.Utc(nowUtc);
        ApprovalBy = Clean(by);
        if (approved)
        {
            Move(TicketStatus.Repairing, nowUtc);
        }
        else
        {
            UnrepairableReason ??= "Müşteri fiyat teklifini onaylamadı.";
            Move(TicketStatus.Unrepairable, nowUtc);
        }

        return Result.Success();
    }

    /// <summary>Workflow step. Repair work needs an approved price when one was asked; delivery has its own step.</summary>
    public Result ChangeStatus(TicketStatus to, string? reason, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (to == TicketStatus.Delivered)
        {
            return Error.Validation("ticket.deliver", "Teslim, teslim ekranından yapılır (ödeme ve imza ile).");
        }

        if (to is TicketStatus.Repairing or TicketStatus.Ready && Approval is ApprovalState.Pending or ApprovalState.Declined)
        {
            return Error.Validation("ticket.approval", Approval == ApprovalState.Pending
                ? "Müşteri fiyat teklifini henüz onaylamadı. Onayı kaydedin ya da teklifi geri çekin."
                : "Müşteri fiyat teklifini onaylamadı; cihaz onarılmadan iade edilir.");
        }

        if (to == TicketStatus.Unrepairable)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Error.Validation("ticket.reason", "Neden onarılmadığını yazın; iade fişinde yer alır.");
            }

            UnrepairableReason = reason.Trim();
        }

        Move(to, nowUtc);
        return Result.Success();
    }

    /// <summary>Price proposal withdrawn (e.g. agreed at the counter after all).</summary>
    public Result WithdrawEstimate(DateTime nowUtc)
    {
        if (!IsOpen || Approval != ApprovalState.Pending)
        {
            return Error.Validation("ticket.no_estimate", "Bekleyen bir fiyat teklifi yok.");
        }

        Approval = ApprovalState.NotAsked;
        EstimateMinor = null;
        Move(TicketStatus.Diagnosing, nowUtc);
        return Result.Success();
    }

    public void LinkWarranty(Guid? earlierTicketId) => WarrantyOfTicketId = earlierTicketId;

    /// <summary>
    /// KVKK erasure of the customer: names and the address go, the device, fault and work stay for the 3-year
    /// service record (SSHY m.16) without identity. Returns the names removed, to clear them from the history too.
    /// </summary>
    public IReadOnlyList<string> ClearPersonalDetails()
    {
        var names = new[] { BroughtBy, DeliveredTo, ApprovalBy, SiteAddress }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
        BroughtBy = null;
        ApprovalBy = null;
        SiteAddress = OnSite ? "(silindi)" : null;
        DeliveredTo = DeliveredTo is null ? null : "(silindi)";
        UnlockCodeProtected = null;
        return names;
    }

    public Result<ServiceTicketLine> AddLine(TicketLineKind kind, Guid? productId, string description, int quantity, long unitPriceMinor, int vatRate, Guid? staffId, Guid? stockMovementId)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Error.Validation("ticket.line", "Satır açıklaması zorunludur.");
        }

        if (quantity is < 1 or > 1000 || unitPriceMinor < 0)
        {
            return Error.Validation("ticket.line_amount", "Miktar 1–1000, fiyat sıfır veya daha büyük olmalı.");
        }

        if (vatRate is not (0 or 1 or 10 or 20))
        {
            return Error.Validation("ticket.vat", "KDV oranı %0, %1, %10 veya %20 olmalı.");
        }

        if (kind == TicketLineKind.Part && productId is null)
        {
            return Error.Validation("ticket.part", "Parça stoktan seçilir.");
        }

        var line = new ServiceTicketLine(Id, _lines.Count + 1, kind, productId, description.Trim(), quantity, unitPriceMinor, vatRate, staffId, stockMovementId);
        _lines.Add(line);
        return line;
    }

    public Result<ServiceTicketLine> RemoveLine(Guid lineId)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId && !l.IsRemoved);
        if (line is null)
        {
            return Error.NotFound("Satır");
        }

        line.MarkRemoved();
        return line;
    }

    /// <summary>What the customer pays at delivery: the lines (no labour within a repair warranty) or, unrepaired, the agreed diagnostic fee.</summary>
    public long AmountDue(bool repaired) => repaired
        ? _lines.Where(l => !l.IsRemoved && !(WarrantyOfTicketId is not null && l.Kind == TicketLineKind.Labor)).Sum(l => l.TotalMinor)
        : DiagnosticFeeMinor;

    public Result Deliver(string deliveredTo, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            return Closed();
        }

        if (Status is not (TicketStatus.Ready or TicketStatus.Unrepairable))
        {
            return Error.Validation("ticket.not_ready", "Teslim için cihaz Hazır ya da Onarılmadı durumunda olmalı.");
        }

        if (string.IsNullOrWhiteSpace(deliveredTo))
        {
            return Error.Validation("ticket.delivered_to", "Teslim alan kişinin adını yazın (SSHY m.11).");
        }

        Repaired = Status == TicketStatus.Ready;
        TotalChargedMinor = AmountDue(Repaired);
        DeliveredTo = deliveredTo.Trim();
        DeliveredAtUtc = Guard.Utc(nowUtc);
        UnlockCodeProtected = null; // no longer needed: erased (KVKK m.7)
        Move(TicketStatus.Delivered, nowUtc);
        return Result.Success();
    }

    public void SetUnlockCode(byte[]? protectedCode) => UnlockCodeProtected = IsOpen ? protectedCode : null;

    /// <summary>Repair warranty end (same fault, 12 months) and parts warranty end (6 months), from delivery.</summary>
    public (DateOnly SameFault, DateOnly Parts)? WarrantyUntil(TimeZoneInfo zone) =>
        DeliveredAtUtc is { } at && Repaired
            ? (TimeZones.LocalDate(at, zone).AddMonths(ServiceRules.SameFaultWarrantyMonths), TimeZones.LocalDate(at, zone).AddMonths(ServiceRules.PartsWarrantyMonths))
            : null;

    private void Move(TicketStatus to, DateTime nowUtc)
    {
        Status = to;
        StatusChangedAtUtc = Guard.Utc(nowUtc);
    }

    private static Error Closed() => Error.Validation("ticket.closed", "Teslim edilmiş servis fişi değiştirilemez.");

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class ServiceTicketLine
{
    private ServiceTicketLine()
    {
    }

    internal ServiceTicketLine(Guid ticketId, int lineNo, TicketLineKind kind, Guid? productId, string description, int quantity, long unitPriceMinor, int vatRate, Guid? staffId, Guid? stockMovementId)
    {
        Id = Ids.New();
        TicketId = ticketId;
        LineNo = lineNo;
        Kind = kind;
        ProductId = productId;
        Description = description;
        Quantity = quantity;
        UnitPriceMinor = unitPriceMinor;
        VatRate = vatRate;
        StaffId = staffId;
        StockMovementId = stockMovementId;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public int LineNo { get; private set; }

    public TicketLineKind Kind { get; private set; }

    public Guid? ProductId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>KDV included.</summary>
    public long UnitPriceMinor { get; private set; }

    public int VatRate { get; private set; }

    /// <summary>The technician who did the work (labour lines).</summary>
    public Guid? StaffId { get; private set; }

    /// <summary>The stock-out of this part (reversed when the line is removed).</summary>
    public Guid? StockMovementId { get; private set; }

    public bool IsRemoved { get; private set; }

    public long TotalMinor => checked(UnitPriceMinor * Quantity);

    internal void MarkRemoved() => IsRemoved = true;
}

/// <summary>Append-only history of a ticket (who did what, when).</summary>
public sealed class ServiceTicketEvent
{
    private ServiceTicketEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public TicketEventKind Kind { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }

    public DateTime AtUtc { get; private set; }

    /// <summary>KVKK erasure: a name in an older history line is replaced (the line itself stays).</summary>
    public void Redact(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Text = Text.Replace(value, "(silindi)", StringComparison.Ordinal);
        }
    }

    public static ServiceTicketEvent Create(Guid ticketId, TicketEventKind kind, string text, Guid? userId, DateTime nowUtc) => new()
    {
        Id = Ids.New(),
        TicketId = ticketId,
        Kind = kind,
        Text = Guard.NotBlank(text).Length > 500 ? text[..500] : text,
        UserId = userId,
        AtUtc = Guard.Utc(nowUtc),
    };
}
