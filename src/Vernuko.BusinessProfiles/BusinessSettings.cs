using Vernuko.Core;

namespace Vernuko.BusinessProfiles;

/// <summary>Single-row business configuration created by the setup wizard.</summary>
public sealed class BusinessSettings : Entity
{
    private BusinessSettings()
    {
    }

    public BusinessSettings(string businessName, BusinessProfileId profileId, string timeZoneId, WeeklySchedule businessHours)
    {
        BusinessName = Guard.NotBlank(businessName).Trim();
        ProfileId = profileId;
        TimeZoneId = Guard.NotBlank(timeZoneId);
        BusinessHours = businessHours;
        Currency = "TRY";
        SlotMinutes = 15;
        AutoLockMinutes = 10;
        CountryCode = "TR";
    }

    public string BusinessName { get; private set; } = string.Empty;

    public BusinessProfileId ProfileId { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public string? TaxOffice { get; private set; }

    public string? TaxNumber { get; private set; }

    public string CountryCode { get; private set; } = "TR";

    public string TimeZoneId { get; private set; } = TimeZones.Turkey;

    public string Currency { get; private set; } = "TRY";

    public WeeklySchedule BusinessHours { get; private set; } = new();

    /// <summary>Granularity of suggested appointment start times.</summary>
    public int SlotMinutes { get; private set; }

    /// <summary>When true, a no-show deducts a session from the linked package (off by default).</summary>
    public bool NoShowConsumesSession { get; private set; }

    /// <summary>New appointments start as "Onaylandı" instead of "Oluşturuldu".</summary>
    public bool AutoConfirmAppointments { get; private set; }

    public int AutoLockMinutes { get; private set; }

    public DateTime? SetupCompletedAtUtc { get; private set; }

    public bool IsDemo { get; private set; }

    public BusinessProfileDefinition Profile => BusinessProfileCatalog.Get(ProfileId);

    public void UpdateContact(string businessName, string? phone, string? email, string? address, string? taxOffice, string? taxNumber)
    {
        BusinessName = Guard.NotBlank(businessName).Trim();
        Phone = Normalize(phone);
        Email = Normalize(email);
        Address = Normalize(address);
        TaxOffice = Normalize(taxOffice);
        TaxNumber = Normalize(taxNumber);
    }

    public Result UpdateHours(WeeklySchedule hours)
    {
        var validation = hours.Validate();
        if (validation.IsFailure)
        {
            return validation;
        }

        BusinessHours = hours;
        return Result.Success();
    }

    public void UpdatePolicies(int slotMinutes, bool noShowConsumesSession, bool autoConfirm, int autoLockMinutes)
    {
        SlotMinutes = slotMinutes is 5 or 10 or 15 or 20 or 30 or 60 ? slotMinutes : 15;
        NoShowConsumesSession = noShowConsumesSession;
        AutoConfirmAppointments = autoConfirm;
        AutoLockMinutes = Math.Clamp(autoLockMinutes, 0, 240);
    }

    public void ChangeTimeZone(string timeZoneId) => TimeZoneId = Guard.NotBlank(timeZoneId);

    public void MarkSetupCompleted(DateTime utcNow, bool isDemo)
    {
        SetupCompletedAtUtc = Guard.Utc(utcNow);
        IsDemo = isDemo;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
