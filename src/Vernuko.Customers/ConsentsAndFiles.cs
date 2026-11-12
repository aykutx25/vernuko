using System.Security.Cryptography;
using System.Text;
using Vernuko.Core;

namespace Vernuko.Customers;

/// <summary>
/// Consent / information form text maintained by the business (e.g. "Lazer epilasyon bilgilendirilmiş onam").
/// Vernuko provides no legal wording; the business prepares the text with its legal adviser.
/// </summary>
public sealed class ConsentTemplate : Entity
{
    public const int MaxBodyLength = 20_000;

    private ConsentTemplate()
    {
    }

    public ConsentTemplate(string title, string body)
    {
        var r = Update(title, body);
        if (r.IsFailure)
        {
            throw new ArgumentException(r.Error.Message);
        }

        IsActive = true;
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>Incremented whenever the text changes, so every acceptance refers to the exact wording.</summary>
    public int TextVersion { get; private set; }

    public bool IsActive { get; private set; }

    public string BodySha256 => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));

    public Result Update(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("consent.title", "Form başlığı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
        {
            return Error.Validation("consent.body", $"Form metni zorunludur (en fazla {MaxBodyLength} karakter).");
        }

        var normalized = body.Trim();
        if (normalized != Body)
        {
            TextVersion++;
        }

        Title = title.Trim();
        Body = normalized;
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;
}

public enum ConsentMethod
{
    /// <summary>Signed on paper; the scan may be attached as a customer file.</summary>
    PaperSigned,

    /// <summary>Accepted verbally in front of staff.</summary>
    Verbal,

    /// <summary>Accepted on screen (tablet signature support is planned).</summary>
    OnScreen,
}

/// <summary>Record that a customer accepted a specific version of a consent text.</summary>
public sealed class CustomerConsent : Entity
{
    private CustomerConsent()
    {
    }

    private CustomerConsent(Guid customerId, ConsentTemplate template, ConsentMethod method, DateTime acceptedAtUtc)
    {
        CustomerId = customerId;
        TemplateId = template.Id;
        TemplateVersion = template.TextVersion;
        Title = template.Title;
        BodySha256 = template.BodySha256;
        Method = method;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public Guid CustomerId { get; private set; }

    public Guid TemplateId { get; private set; }

    public int TemplateVersion { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Fingerprint of the exact text that was accepted.</summary>
    public string BodySha256 { get; private set; } = string.Empty;

    public ConsentMethod Method { get; private set; }

    public DateTime AcceptedAtUtc { get; private set; }

    public Guid? RecordedByUserId { get; private set; }

    public Guid? ScanFileId { get; private set; }

    public string? Note { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? RevokeReason { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    public static CustomerConsent Record(Guid customerId, ConsentTemplate template, ConsentMethod method, Guid? userId, Guid? scanFileId, string? note, DateTime nowUtc) =>
        new(customerId, template, method, Guard.Utc(nowUtc))
        {
            RecordedByUserId = userId,
            ScanFileId = scanFileId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };

    public Result Revoke(string reason, DateTime nowUtc)
    {
        if (!IsActive)
        {
            return Error.Validation("consent.revoked", "Bu onam zaten geri alınmış.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("consent.revoke_reason", "Geri alma nedeni zorunludur.");
        }

        RevokedAtUtc = Guard.Utc(nowUtc);
        RevokeReason = reason.Trim();
        return Result.Success();
    }

    public static string MethodName(ConsentMethod method) => method switch
    {
        ConsentMethod.PaperSigned => "Kâğıt üzerinde imzalı",
        ConsentMethod.Verbal => "Sözlü",
        _ => "Ekranda onay",
    };
}

public enum CustomerFileKind
{
    Photo,
    ConsentScan,
    Document,
}

/// <summary>
/// Metadata of an encrypted customer attachment (before/after photo, signed consent scan ...).
/// The content is stored encrypted on disk, never in plain text, and is included in encrypted backups.
/// </summary>
public sealed class CustomerFile : Entity
{
    public const long MaxBytes = 25L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf", ".heic", ".webp" };

    private CustomerFile()
    {
    }

    private CustomerFile(Guid customerId, CustomerFileKind kind, string originalName, long size, string sha256)
    {
        CustomerId = customerId;
        Kind = kind;
        OriginalName = originalName;
        SizeBytes = size;
        Sha256 = sha256;
        StoredName = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) + ".vatt";
    }

    public Guid CustomerId { get; private set; }

    public CustomerFileKind Kind { get; private set; }

    public string OriginalName { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>SHA-256 of the plain content (integrity check after decryption).</summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Random file name of the encrypted blob (no personal data in file names).</summary>
    public string StoredName { get; private set; } = string.Empty;

    public Guid? AppointmentId { get; private set; }

    public string? Note { get; private set; }

    public Guid? UploadedByUserId { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsImage => Path.GetExtension(OriginalName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";

    public static Result<CustomerFile> Create(Guid customerId, CustomerFileKind kind, string originalName, ReadOnlySpan<byte> content, Guid? appointmentId, string? note, Guid? userId)
    {
        var name = Path.GetFileName(originalName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Error.Validation("file.name", "Dosya adı geçersiz.");
        }

        if (!AllowedExtensions.Contains(Path.GetExtension(name)))
        {
            return Error.Validation("file.type", "Yalnızca JPG, PNG, WEBP, HEIC ve PDF dosyaları eklenebilir.");
        }

        if (content.Length == 0 || content.Length > MaxBytes)
        {
            return Error.Validation("file.size", "Dosya boş olamaz ve 25 MB'tan büyük olamaz.");
        }

        return new CustomerFile(customerId, kind, name, content.Length, Convert.ToHexStringLower(SHA256.HashData(content)))
        {
            AppointmentId = appointmentId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UploadedByUserId = userId,
        };
    }

    public Result Delete(DateTime nowUtc)
    {
        if (DeletedAtUtc is not null)
        {
            return Error.Validation("file.deleted", "Dosya zaten silinmiş.");
        }

        DeletedAtUtc = Guard.Utc(nowUtc);
        return Result.Success();
    }

    public static string KindName(CustomerFileKind kind) => kind switch
    {
        CustomerFileKind.Photo => "Fotoğraf",
        CustomerFileKind.ConsentScan => "İmzalı onam",
        _ => "Belge",
    };
}
