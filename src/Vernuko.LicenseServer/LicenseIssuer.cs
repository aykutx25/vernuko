using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vernuko.Licensing;

namespace Vernuko.LicenseServer;

/// <summary>Outcome of a public endpoint: either a signed license or a Turkish message with a stable code.</summary>
public sealed record IssueResult(string? SignedLicense, string? ActivationId, string? ErrorCode, string? Message, int HttpStatus)
{
    public bool Ok => SignedLicense is not null;

    public static IssueResult Fail(int status, string code, string message) => new(null, null, code, message, status);
}

public sealed record UpdatePermission(bool Allowed, string Message);

/// <summary>
/// Activation, refresh, deactivation, server-side trial and update permission. The rules mirror the desktop's
/// evaluator: nothing here can delete or lock customer data — a refused refresh only lets the existing license
/// document run out, after which the app becomes read-only.
/// </summary>
public sealed class LicenseIssuer(LicenseServerDb db, ServerSecrets secrets, LicenseServerOptions options, TimeProvider time)
{
    public const string Product = "vernuko-desktop";

    /// <summary>
    /// Seat check and the new activation are one step: two activations at the same moment on a one-seat license
    /// both saw a free seat. The server is a single process, so an in-process lock is enough.
    /// </summary>
    private static readonly SemaphoreSlim SeatGate = new(1, 1);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<IssueResult> ActivateAsync(string? licenseKey, string? deviceHash, string? machineLabel, CancellationToken ct)
    {
        await SeatGate.WaitAsync(ct);
        try
        {
            return await ActivateCoreAsync(licenseKey, deviceHash, machineLabel, ct);
        }
        finally
        {
            SeatGate.Release();
        }
    }

    private async Task<IssueResult> ActivateCoreAsync(string? licenseKey, string? deviceHash, string? machineLabel, CancellationToken ct)
    {
        if (!ValidDevice(deviceHash))
        {
            return IssueResult.Fail(400, "request.device", "Cihaz bilgisi eksik.");
        }

        var normalized = LicenseKeys.Normalize(licenseKey);
        if (normalized is null)
        {
            return IssueResult.Fail(400, "license.key_format", "Lisans anahtarı VRNK-XXXXX-XXXXX-XXXXX-XXXXX biçiminde olmalı.");
        }

        var hash = LicenseKeys.Hash(normalized);
        var license = await db.Licenses.Include(l => l.Activations).FirstOrDefaultAsync(l => l.KeyHash == hash, ct);
        if (license is null)
        {
            return IssueResult.Fail(404, "license.not_found", "Bu lisans anahtarı bulunamadı. Anahtarı kontrol edin.");
        }

        if (EffectiveEnd(license) <= Now)
        {
            return IssueResult.Fail(403, "license.ended", EndedMessage(license));
        }

        var activation = license.Activations.FirstOrDefault(a => a.IsActive && a.DeviceHash == deviceHash);
        if (activation is null)
        {
            var used = license.Activations.Count(a => a.IsActive);
            if (used >= license.MaxDevices)
            {
                return IssueResult.Fail(409, "license.seats",
                    $"Bu lisans {license.MaxDevices} bilgisayar içindir ve şu an başka bilgisayarda etkin. Eski bilgisayarda Ayarlar › Lisans › \"Bu bilgisayardan kaldır\" yapın veya destekle iletişime geçin.");
            }

            activation = new Activation
            {
                Id = LicenseKeys.NewActivationId(),
                LicenseId = license.Id,
                DeviceHash = deviceHash!,
                MachineLabel = Trim(machineLabel, 100),
                CreatedAtUtc = Now,
            };
            db.Activations.Add(activation);
            Audit("activation.created", license.Id, $"{activation.Id} {activation.MachineLabel}");
        }

        activation.LastSeenUtc = Now;
        await db.SaveChangesAsync(ct);
        return new IssueResult(Sign(license, activation), activation.Id, null, null, 200);
    }

    public async Task<IssueResult> RefreshAsync(string? activationId, string? deviceHash, CancellationToken ct)
    {
        var (license, activation, error) = await FindActivationAsync(activationId, deviceHash, ct);
        if (error is not null)
        {
            return error;
        }

        if (EffectiveEnd(license!) <= Now)
        {
            return IssueResult.Fail(403, "license.ended", EndedMessage(license!));
        }

        activation!.LastSeenUtc = Now;
        await db.SaveChangesAsync(ct);
        return new IssueResult(Sign(license!, activation), activation.Id, null, null, 200);
    }

    /// <summary>Frees the seat (moving to a new computer). The app deletes its local license file itself.</summary>
    public async Task<IssueResult> DeactivateAsync(string? activationId, string? deviceHash, CancellationToken ct)
    {
        var (license, activation, error) = await FindActivationAsync(activationId, deviceHash, ct);
        if (error is not null)
        {
            return error;
        }

        activation!.DeactivatedAtUtc = Now;
        Audit("activation.deactivated", license!.Id, $"{activation.Id} (müşteri)");
        await db.SaveChangesAsync(ct);
        return new IssueResult(string.Empty, activation.Id, null, "Lisans bu bilgisayardan kaldırıldı.", 200);
    }

    /// <summary>First trial start ever seen for this device (idempotent).</summary>
    public async Task<(DateTime? StartedAtUtc, IssueResult? Error)> TrialAsync(string? deviceHash, CancellationToken ct)
    {
        if (!ValidDevice(deviceHash))
        {
            return (null, IssueResult.Fail(400, "request.device", "Cihaz bilgisi eksik."));
        }

        var record = await db.Trials.FirstOrDefaultAsync(t => t.DeviceHash == deviceHash, ct);
        if (record is null)
        {
            record = new TrialRecord { DeviceHash = deviceHash!, StartedAtUtc = Now };
            db.Trials.Add(record);
            await db.SaveChangesAsync(ct);
        }

        return (record.StartedAtUtc, null);
    }

    public async Task<UpdatePermission> UpdatePermissionAsync(string? activationId, string? deviceHash, CancellationToken ct)
    {
        var (license, _, error) = await FindActivationAsync(activationId, deviceHash, ct);
        if (error is not null)
        {
            return new UpdatePermission(false, error.Message!);
        }

        return EffectiveEnd(license!) > Now
            ? new UpdatePermission(true, "Güncellemeler açık.")
            : new UpdatePermission(false, "Güncellemeler etkin bir abonelikle alınır. Mevcut sürüm ve verileriniz çalışmaya devam eder.");
    }

    /// <summary>
    /// Paid period + payment tolerance while the subscription is running; exactly the paid period once it is
    /// cancelled or paused (no surprise extra days, no early cut-off either).
    /// </summary>
    public DateTime EffectiveEnd(ServerLicense license) => license.Status is ServerLicenseStatus.Active or ServerLicenseStatus.PastDue
        ? license.PaidUntilUtc.AddDays(options.PaymentToleranceDays)
        : license.PaidUntilUtc;

    public string Sign(ServerLicense license, Activation activation)
    {
        var validUntil = EffectiveEnd(license);
        var refresh = Now.AddDays(options.RefreshDays);
        var doc = new LicenseDocument
        {
            LicenseId = license.Id,
            Product = Product,
            Edition = license.Edition,
            Features = license.Features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ActivationId = activation.Id,
            DeviceHash = activation.DeviceHash,
            MaxDevices = license.MaxDevices,
            IssuedAtUtc = Now,
            ValidUntilUtc = validUntil,
            RefreshAfterUtc = refresh < validUntil ? refresh : validUntil,
            OfflineGraceDays = options.OfflineGraceDays,
        };
        return LicenseVerifier.Sign(doc, secrets.SigningKey, secrets.KeyId);
    }

    public void Audit(string action, string? licenseId, string detail) =>
        db.Audit.Add(new ServerAuditEntry { AtUtc = Now, Action = action, LicenseId = licenseId, Detail = Trim(detail, 1000) ?? string.Empty });

    private async Task<(ServerLicense? License, Activation? Activation, IssueResult? Error)> FindActivationAsync(string? activationId, string? deviceHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(activationId) || !ValidDevice(deviceHash))
        {
            return (null, null, IssueResult.Fail(400, "request.activation", "Etkinleştirme bilgisi eksik."));
        }

        var activation = await db.Activations.FirstOrDefaultAsync(a => a.Id == activationId, ct);
        if (activation is null || activation.DeviceHash != deviceHash)
        {
            return (null, null, IssueResult.Fail(404, "activation.not_found", "Bu bilgisayarın etkinleştirmesi bulunamadı. Lisans anahtarıyla yeniden etkinleştirin."));
        }

        if (!activation.IsActive)
        {
            return (null, null, IssueResult.Fail(403, "activation.removed", "Lisans bu bilgisayardan kaldırılmış. Lisans anahtarıyla yeniden etkinleştirebilirsiniz."));
        }

        var license = await db.Licenses.FirstAsync(l => l.Id == activation.LicenseId, ct);
        return (license, activation, null);
    }

    private static string EndedMessage(ServerLicense license) =>
        $"Lisansın süresi {license.PaidUntilUtc.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} tarihinde doldu. Verileriniz silinmez; yenileme sonrası devam edebilirsiniz.";

    private static bool ValidDevice(string? deviceHash) => deviceHash is { Length: >= 16 and <= 128 } && deviceHash.All(char.IsAsciiLetterOrDigit);

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
