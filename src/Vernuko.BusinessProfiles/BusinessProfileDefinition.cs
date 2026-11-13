using Vernuko.Core;

namespace Vernuko.BusinessProfiles;

/// <param name="ResourceKeywords">Suggested rooms/devices whose name contains one of these words are linked to the service at setup.</param>
/// <param name="ProcessingStartMinutes">Waiting part (e.g. hair dye): staff free from this minute for ProcessingMinutes; the chair stays occupied.</param>
public sealed record ServiceTemplate(string Name, string Category, int DurationMinutes, int PrepMinutes = 0, int CleanupMinutes = 0, ResourceKind? RequiredResource = null, IReadOnlyList<string>? ResourceKeywords = null, int ProcessingStartMinutes = 0, int ProcessingMinutes = 0);

public sealed record ResourceTemplate(string Name, ResourceKind Kind);

public sealed record PackageTemplateSuggestion(string Name, string ServiceName, int Sessions, int? ValidityDays);

/// <summary>Words the UI uses for this vertical ("Müşteri" vs "Hasta" vs "Danışan" ...).</summary>
public sealed record Terminology(
    string Customer,
    string Customers,
    string StaffMember,
    string Staff,
    string Resource,
    string Resources);

public sealed record BusinessProfileDefinition(
    BusinessProfileId Id,
    string DisplayName,
    string Description,
    Terminology Terms,
    IReadOnlySet<ProfileFeature> Features,
    IReadOnlyList<ResourceKind> ResourceKinds,
    IReadOnlyList<ServiceTemplate> SuggestedServices,
    IReadOnlyList<ResourceTemplate> SuggestedResources,
    IReadOnlyList<PackageTemplateSuggestion> SuggestedPackages,
    DataSensitivity Sensitivity,
    bool IsAvailable)
{
    public bool Has(ProfileFeature feature) => Features.Contains(feature);

    /// <summary>For special-category data (health, therapy) reminders must not reveal the service name.</summary>
    public bool HideServiceNameInMessages => Sensitivity == DataSensitivity.SpecialCategory;
}
