using Vernuko.Core;

namespace Vernuko.Customers;

public enum DuplicateKind
{
    /// <summary>Same phone and same name: very likely the same person.</summary>
    LikelySamePerson,

    /// <summary>Same phone, different name: often a family member sharing a phone.</summary>
    SharedPhone,

    /// <summary>Same name, different or missing phone.</summary>
    SameName,
}

public sealed record DuplicateCandidate(Guid CustomerId, int Number, string FullName, string? Phone, DuplicateKind Kind)
{
    public string Explanation => Kind switch
    {
        DuplicateKind.LikelySamePerson => "Aynı ad ve telefonla kayıtlı bir müşteri var.",
        DuplicateKind.SharedPhone => "Bu telefon numarası başka bir müşteride kayıtlı (aile üyesi olabilir).",
        _ => "Aynı isimde bir müşteri var.",
    };
}

/// <summary>
/// Finds possible duplicates. It never merges: the user decides (two different people can share a phone).
/// </summary>
public static class DuplicateDetector
{
    public static IReadOnlyList<DuplicateCandidate> Find(string fullName, string? phoneE164, IEnumerable<Customer> existing, Guid? excludeId = null)
    {
        var nameKey = TurkishText.ToSearchKey(fullName);
        var result = new List<DuplicateCandidate>();
        foreach (var c in existing)
        {
            if (c.Id == excludeId || c.State == CustomerState.Anonymized)
            {
                continue;
            }

            var sameName = TurkishText.ToSearchKey(c.FullName) == nameKey;
            var samePhone = phoneE164 is not null && c.Phone == phoneE164;
            if (samePhone && sameName)
            {
                result.Add(new DuplicateCandidate(c.Id, c.Number, c.FullName, c.DisplayPhone, DuplicateKind.LikelySamePerson));
            }
            else if (samePhone)
            {
                result.Add(new DuplicateCandidate(c.Id, c.Number, c.FullName, c.DisplayPhone, DuplicateKind.SharedPhone));
            }
            else if (sameName)
            {
                result.Add(new DuplicateCandidate(c.Id, c.Number, c.FullName, c.DisplayPhone, DuplicateKind.SameName));
            }
        }

        return result.OrderBy(r => r.Kind).ToList();
    }
}
