using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed record PrivateNoteDto(Guid Id, DateTime CreatedAtUtc, string Text);

/// <param name="Mine">The signed-in user's own notes (decrypted).</param>
/// <param name="OthersCount">Notes of other specialists on this customer — never shown, only counted.</param>
public sealed record PrivateNotesView(IReadOnlyList<PrivateNoteDto> Mine, int OthersCount);

/// <summary>
/// Gizli uzman notları: encrypted at rest with the attachment key (bound to each note id), readable only by the
/// author. Reading, writing and deleting are audited without any note content.
/// </summary>
public sealed class PrivateNotesService(AppServiceDeps deps, AttachmentKeyStore keys) : AppServiceBase(deps), IPrivateNotesService
{
    public Task<Result<PrivateNotesView>> GetAsync(Guid customerId, CancellationToken ct = default) =>
        WriteAlwaysAsync<PrivateNotesView>(Permission.PrivateNotesViewOwn, async db =>
        {
            if (UserId is not { } me)
            {
                return Error.Forbidden;
            }

            var notes = await db.PrivateNotes.AsNoTracking().Where(n => n.CustomerId == customerId && n.DeletedAtUtc == null).OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
            var mine = notes.Where(n => n.AuthorUserId == me).ToList();
            var result = new List<PrivateNoteDto>();
            if (mine.Count > 0)
            {
                var key = await keys.TryLoadAsync(db, ct);
                if (key is null)
                {
                    return new Error("note.key", "Not anahtarı bu bilgisayarda açılamıyor. Yedeği kurtarma anahtarıyla geri yükleyin.");
                }

                try
                {
                    foreach (var n in mine)
                    {
                        var plain = AttachmentCrypto.Decrypt(key, n.Id, n.Ciphertext);
                        result.Add(new PrivateNoteDto(n.Id, n.CreatedAtUtc, plain is null ? "(Not çözülemedi; veri bozulmuş olabilir.)" : Encoding.UTF8.GetString(plain)));
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(key);
                }

                await Audit.AppendAsync(db, "private_note.viewed", "Customer", customerId, $"{mine.Count} gizli not görüntülendi (yazarı)", ct);
            }

            return new PrivateNotesView(result, notes.Count - mine.Count);
        }, ct);

    public Task<Result<Guid>> AddAsync(Guid customerId, string text, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PrivateNotesViewOwn, async db =>
        {
            if (UserId is not { } me)
            {
                return Error.Forbidden;
            }

            if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.State != CustomerState.Anonymized, ct))
            {
                return Error.NotFound("Müşteri");
            }

            if (await keys.GetOrCreateAsync(db, UtcNow, ct) is not { } key)
            {
                return AttachmentKeyStore.KeyUnavailable;
            }

            try
            {
                var created = PrivateNote.Create(customerId, me, text?.Trim() ?? string.Empty, id => AttachmentCrypto.Encrypt(key, id, Encoding.UTF8.GetBytes(text!.Trim())), UtcNow);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                db.PrivateNotes.Add(created.Value);
                await Audit.AppendAsync(db, "private_note.created", "Customer", customerId, "Gizli not eklendi", ct);
                return created.Value.Id;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }, ct);

    /// <summary>Only the author can delete; the encrypted text is wiped.</summary>
    public Task<Result<bool>> DeleteAsync(Guid noteId, CancellationToken ct = default) =>
        WriteAlwaysAsync<bool>(Permission.PrivateNotesViewOwn, async db =>
        {
            var note = await db.PrivateNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DeletedAtUtc == null, ct);
            if (note is null || note.AuthorUserId != UserId)
            {
                return Error.NotFound("Not");
            }

            note.Delete(UtcNow);
            await Audit.AppendAsync(db, "private_note.deleted", "Customer", note.CustomerId, "Gizli not silindi (yazarı)", ct);
            return true;
        }, ct);
}
