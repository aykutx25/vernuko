using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>
/// Consent records and encrypted customer attachments (photos, signed consent scans). Photos are personal data and
/// may be special-category data (health/aesthetics): viewing requires a dedicated permission and is audited.
/// The legal basis and consent wording must be prepared by the business with its legal adviser.
/// </summary>
public sealed class CustomerRecordsService(AppServiceDeps deps, AttachmentKeyStore keys, IAppEnvironment env) : AppServiceBase(deps), ICustomerRecordsService
{
    // ----- Consent templates -----
    public Task<Result<IReadOnlyList<ConsentTemplateDto>>> GetTemplatesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ConsentTemplateDto>>(Permission.ConsentsRecord, async db =>
            (await db.ConsentTemplates.AsNoTracking().Where(t => includeInactive || t.IsActive).OrderBy(t => t.Title).ToListAsync(ct))
                .Select(t => new ConsentTemplateDto(t.Id, t.Title, t.Body, t.TextVersion, t.IsActive)).ToList(), ct);

    public Task<Result<Guid>> SaveTemplateAsync(ConsentTemplateDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.SettingsManage, async db =>
        {
            ConsentTemplate? t;
            if (dto.Id == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body))
                {
                    return Error.Validation("consent.template", "Başlık ve metin zorunludur.");
                }

                t = new ConsentTemplate(dto.Title, dto.Body);
                db.ConsentTemplates.Add(t);
            }
            else
            {
                t = await db.ConsentTemplates.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (t is null)
                {
                    return Error.NotFound("Onam formu");
                }

                var r = t.Update(dto.Title, dto.Body);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            t.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, "consent_template.saved", "ConsentTemplate", t.Id, $"Onam formu kaydedildi: {t.Title} (sürüm {t.TextVersion})", ct);
            return t.Id;
        }, ct);

    // ----- Consents -----
    public Task<Result<IReadOnlyList<CustomerConsentDto>>> GetConsentsAsync(Guid customerId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CustomerConsentDto>>(Permission.CustomersView, async db =>
        {
            var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            return (await db.CustomerConsents.AsNoTracking().Where(c => c.CustomerId == customerId).OrderByDescending(c => c.AcceptedAtUtc).ToListAsync(ct))
                .Select(c => new CustomerConsentDto(c.Id, c.Title, c.TemplateVersion, c.Method, c.AcceptedAtUtc,
                    c.RecordedByUserId is { } u ? users.GetValueOrDefault(u) : null, c.Note, c.RevokedAtUtc, c.RevokeReason)).ToList();
        }, ct);

    public Task<Result<Guid>> RecordConsentAsync(Guid customerId, Guid templateId, ConsentMethod method, string? note, Guid? scanFileId = null, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.ConsentsRecord, async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("consent.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            var template = await db.ConsentTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
            if (template is null || !template.IsActive)
            {
                return Error.Validation("consent.template", "Onam formu bulunamadı veya pasif.");
            }

            var consent = CustomerConsent.Record(customerId, template, method, UserId, scanFileId, note, UtcNow);
            db.CustomerConsents.Add(consent);
            await Audit.AppendAsync(db, "consent.recorded", "Customer", customerId, $"Onam kaydedildi: {template.Title} (sürüm {template.TextVersion}, {CustomerConsent.MethodName(method)})", ct);
            return consent.Id;
        }, ct);

    public Task<Result<bool>> RevokeConsentAsync(Guid consentId, string reason, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.ConsentsRecord, async db =>
        {
            var consent = await db.CustomerConsents.FirstOrDefaultAsync(c => c.Id == consentId, ct);
            if (consent is null)
            {
                return Error.NotFound("Onam kaydı");
            }

            var r = consent.Revoke(reason, UtcNow);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "consent.revoked", "Customer", consent.CustomerId, $"Onam geri alındı: {consent.Title}", ct);
            return true;
        }, ct);

    // ----- Encrypted files -----
    public Task<Result<IReadOnlyList<CustomerFileDto>>> GetFilesAsync(Guid customerId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CustomerFileDto>>(Permission.CustomerFilesView, async db =>
            (await db.CustomerFiles.AsNoTracking().Where(f => f.CustomerId == customerId && f.DeletedAtUtc == null).OrderByDescending(f => f.CreatedAtUtc).ToListAsync(ct))
                .Select(f => new CustomerFileDto(f.Id, f.Kind, f.OriginalName, f.SizeBytes, f.CreatedAtUtc, f.Note, f.IsImage)).ToList(), ct);

    public Task<Result<Guid>> AddFileAsync(Guid customerId, CustomerFileKind kind, string originalName, byte[] content, string? note, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.CustomerFilesManage, async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("file.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            var created = CustomerFile.Create(customerId, kind, originalName, content, null, note, UserId);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var file = created.Value;
            if (await keys.GetOrCreateAsync(db, UtcNow, ct) is not { } key)
            {
                return AttachmentKeyStore.KeyUnavailable;
            }

            var encrypted = AttachmentCrypto.Encrypt(key, file.Id, content);
            CryptographicOperations.ZeroMemory(key);
            Directory.CreateDirectory(env.AttachmentsDirectory);
            var path = Path.Combine(env.AttachmentsDirectory, file.StoredName);
            var partial = path + ".partial";
            await File.WriteAllBytesAsync(partial, encrypted, ct);
            File.Move(partial, path);
            db.CustomerFiles.Add(file);
            await Audit.AppendAsync(db, "file.added", "Customer", customerId, $"{CustomerFile.KindName(kind)} eklendi ({file.SizeBytes / 1024} KB)", ct);
            return file.Id;
        }, ct);

    /// <summary>Decrypts an attachment into memory (never to a plain file). Every view is audited.</summary>
    public Task<Result<byte[]>> ReadFileAsync(Guid fileId, CancellationToken ct = default) =>
        WriteAlwaysAsync<byte[]>(Permission.CustomerFilesView, async db =>
        {
            var file = await db.CustomerFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId && f.DeletedAtUtc == null, ct);
            if (file is null)
            {
                return Error.NotFound("Dosya");
            }

            var path = Path.Combine(env.AttachmentsDirectory, file.StoredName);
            if (!File.Exists(path))
            {
                return new Error("file.missing", "Dosya diskte bulunamadı. Yedekten geri yükleme gerekebilir.");
            }

            var key = await keys.TryLoadAsync(db, ct);
            if (key is null)
            {
                return new Error("file.key", "Dosya anahtarı bu bilgisayarda açılamıyor. Yedeği kurtarma anahtarıyla geri yükleyin.");
            }

            var plain = AttachmentCrypto.Decrypt(key, file.Id, await File.ReadAllBytesAsync(path, ct));
            CryptographicOperations.ZeroMemory(key);
            if (plain is null || Convert.ToHexStringLower(SHA256.HashData(plain)) != file.Sha256)
            {
                return new Error("file.corrupt", "Dosya bozuk veya değiştirilmiş.");
            }

            await Audit.AppendAsync(db, "file.viewed", "Customer", file.CustomerId, $"{CustomerFile.KindName(file.Kind)} görüntülendi", ct);
            return plain;
        }, ct);

    public Task<Result<bool>> DeleteFileAsync(Guid fileId, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.CustomerFilesManage, async db =>
        {
            var file = await db.CustomerFiles.FirstOrDefaultAsync(f => f.Id == fileId, ct);
            if (file is null)
            {
                return Error.NotFound("Dosya");
            }

            var r = file.Delete(UtcNow);
            if (r.IsFailure)
            {
                return r.Error;
            }

            var storedName = file.StoredName;
            AfterCommit(() => DeleteBlob(env, storedName));
            await Audit.AppendAsync(db, "file.deleted", "Customer", file.CustomerId, $"{CustomerFile.KindName(file.Kind)} silindi", ct);
            return true;
        }, ct);

    /// <summary>Used by anonymisation: removes every attachment of a customer.</summary>
    internal static async Task<int> DeleteAllForCustomerAsync(VernukoDbContext db, IAppEnvironment env, Guid customerId, DateTime nowUtc, CancellationToken ct)
    {
        var files = await db.CustomerFiles.Where(f => f.CustomerId == customerId && f.DeletedAtUtc == null).ToListAsync(ct);
        foreach (var f in files)
        {
            f.Delete(nowUtc);
            var storedName = f.StoredName;
            AfterCommit(() => DeleteBlob(env, storedName));
        }

        return files.Count;
    }

    private static void DeleteBlob(IAppEnvironment env, string storedName)
    {
        var path = Path.Combine(env.AttachmentsDirectory, storedName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
