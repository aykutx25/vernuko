using Vernuko.Core;

namespace Vernuko.Customers;

/// <summary>
/// Gizli uzman notu (psikolog, danışman, avukat): stored ENCRYPTED (AES-GCM with the attachment key, bound to the
/// note id) and readable only by its author — not by the secretary, not by other specialists, not by the owner.
/// Deleting wipes the encrypted text; the row stays so the audit trail remains consistent.
/// </summary>
public sealed class PrivateNote : Entity
{
    public const int MaxLength = 10_000;

    private PrivateNote()
    {
    }

    public Guid CustomerId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public byte[] Ciphertext { get; private set; } = [];

    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

    public static Result<PrivateNote> Create(Guid customerId, Guid authorUserId, string text, Func<Guid, byte[]> encrypt, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("note.empty", "Not boş olamaz.");
        }

        if (text.Length > MaxLength)
        {
            return Error.Validation("note.length", $"Not en fazla {MaxLength} karakter olabilir.");
        }

        var note = new PrivateNote { CustomerId = customerId, AuthorUserId = authorUserId, CreatedAtUtc = Guard.Utc(nowUtc) };
        note.Ciphertext = encrypt(note.Id);
        return note;
    }

    public void Delete(DateTime nowUtc)
    {
        Ciphertext = [];
        DeletedAtUtc = Guard.Utc(nowUtc);
    }
}
