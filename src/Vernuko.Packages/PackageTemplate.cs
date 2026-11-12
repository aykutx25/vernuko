using Vernuko.Core;

namespace Vernuko.Packages;

/// <summary>A sellable package definition, e.g. "Lazer Epilasyon 8 Seans".</summary>
public sealed class PackageTemplate : Entity
{
    private PackageTemplate()
    {
    }

    public PackageTemplate(string name, Guid serviceId, int sessionCount, long priceMinor, int? validityDays)
    {
        var r = Update(name, serviceId, sessionCount, priceMinor, validityDays);
        if (r.IsFailure)
        {
            throw new ArgumentException(r.Error.Message);
        }

        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public Guid ServiceId { get; private set; }

    public int SessionCount { get; private set; }

    public long PriceMinor { get; private set; }

    public int? ValidityDays { get; private set; }

    public bool IsActive { get; private set; }

    public Result Update(string name, Guid serviceId, int sessionCount, long priceMinor, int? validityDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("package.name", "Paket adı zorunludur.");
        }

        if (sessionCount is < 1 or > 500)
        {
            return Error.Validation("package.sessions", "Seans sayısı 1 ile 500 arasında olmalıdır.");
        }

        if (priceMinor < 0)
        {
            return Error.Validation("package.price", "Paket fiyatı negatif olamaz.");
        }

        if (validityDays is < 1 or > 3650)
        {
            return Error.Validation("package.validity", "Geçerlilik süresi 1 ile 3650 gün arasında olmalıdır.");
        }

        Name = name.Trim();
        ServiceId = serviceId;
        SessionCount = sessionCount;
        PriceMinor = priceMinor;
        ValidityDays = validityDays;
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;
}
