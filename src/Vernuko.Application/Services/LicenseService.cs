using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Licensing;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed class LicensingOptions
{
    /// <summary>Trusted license-signing public keys (key id → SubjectPublicKeyInfo DER). Never private keys.</summary>
    public IReadOnlyDictionary<string, byte[]> PublicKeys { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>
    /// License server (HTTPS; plain HTTP only for a loopback test server). Null: online activation is off and
    /// licenses are loaded from a file.
    /// </summary>
    public Uri? ServerUrl { get; init; }
}

/// <summary>
/// Evaluates the license locally (no network needed). Expiry switches the app to read-only mode;
/// it never deletes or locks customer data.
/// </summary>
public sealed class LicenseService(IDbContextFactory<VernukoDbContext> factory, TimeProvider time, IDeviceIdentity device, LicensingOptions options, UserSession session, AuditWriter audit) : ILicenseGate, ILicenseService
{
    private readonly LicenseVerifier _verifier = new(options.PublicKeys);

    public LicenseStatus Current { get; private set; } =
        new(LicenseState.Trial, AccessMode.Full, null, LicenseEvaluator.TrialDays, false, "Deneme sürümü.");

    public bool CanWrite => Current.CanWrite;

    public event EventHandler? Changed;

    public async Task<LicenseStatus> RefreshAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var trialStart = await SettingsStore.GetUtcAsync(db, SettingKeys.LicenseTrialStartedUtc, ct);
        if (trialStart is null)
        {
            // Setup not completed yet: nothing to evaluate, the app is fully usable for setup.
            Current = new LicenseStatus(LicenseState.Trial, AccessMode.Full, now.AddDays(LicenseEvaluator.TrialDays), LicenseEvaluator.TrialDays, false, "Deneme sürümü.");
            return Current;
        }

        var highWater = await SettingsStore.GetUtcAsync(db, SettingKeys.LicenseHighWaterMarkUtc, ct) ?? trialStart.Value;
        var signed = await SettingsStore.GetAsync(db, SettingKeys.LicenseSigned, ct);
        var local = new LicenseLocalState(trialStart.Value, highWater, signed);

        LicenseDocument? doc = null;
        var valid = true;
        if (signed is not null)
        {
            var verified = _verifier.Verify(signed);
            valid = verified.IsSuccess;
            doc = verified.IsSuccess ? verified.Value : null;
        }

        Current = LicenseEvaluator.Evaluate(local, doc, device.DeviceHash, now, valid);
        var advanced = LicenseEvaluator.AdvanceHighWaterMark(local, now);
        if (advanced.HighWaterMarkUtc != highWater)
        {
            await SettingsStore.SetUtcAsync(db, SettingKeys.LicenseHighWaterMarkUtc, advanced.HighWaterMarkUtc, now, ct);
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        Changed?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    /// <summary>Imports a signed license file (offline activation). Requires the LicenseManage permission.</summary>
    public async Task<Result<LicenseStatus>> ImportAsync(string signedLicenseJson, CancellationToken ct = default)
    {
        if (!session.Has(Permission.LicenseManage))
        {
            return Error.Forbidden;
        }

        return await StoreAsync(signedLicenseJson, "license.imported", "yüklendi", ct);
    }

    /// <summary>
    /// Saves a license document received from the license server (activation or periodic refresh). No permission
    /// check: the signature and device are verified, and a refresh must also work for a receptionist's session.
    /// </summary>
    public Task<Result<LicenseStatus>> StoreFromServerAsync(string signedLicenseJson, bool activation, CancellationToken ct = default) =>
        StoreAsync(signedLicenseJson, activation ? "license.activated" : "license.refreshed", activation ? "etkinleştirildi" : "yenilendi", ct);

    /// <summary>The verified license document on this computer, if any.</summary>
    public async Task<LicenseDocument?> GetDocumentAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var signed = await SettingsStore.GetAsync(db, SettingKeys.LicenseSigned, ct);
        return signed is not null && _verifier.Verify(signed) is { IsSuccess: true } v ? v.Value : null;
    }

    /// <summary>After the seat was freed on the server: forget the local license (data stays; app becomes read-only).</summary>
    public async Task<LicenseStatus> RemoveLocalAsync(string reason, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SettingsStore.RemoveAsync(db, SettingKeys.LicenseSigned, ct);
            await audit.AppendAsync(db, "license.removed", "License", null, reason, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await RefreshAsync(ct);
    }

    /// <summary>
    /// The server remembers the first trial start of this computer; an earlier date there wins, so reinstalling
    /// cannot restart the trial. A later server date is ignored (never shortens a running trial unfairly).
    /// </summary>
    public async Task<LicenseStatus> AdoptServerTrialStartAsync(DateTime serverStartUtc, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var local = await SettingsStore.GetUtcAsync(db, SettingKeys.LicenseTrialStartedUtc, ct);
            if (local is { } l && serverStartUtc < l && serverStartUtc <= now)
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await SettingsStore.SetUtcAsync(db, SettingKeys.LicenseTrialStartedUtc, serverStartUtc, now, ct);
                await audit.AppendAsync(db, "license.trial_synced", "License", null, $"Deneme başlangıcı sunucudaki kayda göre {serverStartUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
        }

        return await RefreshAsync(ct);
    }

    private async Task<Result<LicenseStatus>> StoreAsync(string signedLicenseJson, string action, string verb, CancellationToken ct)
    {
        var verified = _verifier.Verify(signedLicenseJson);
        if (verified.IsFailure)
        {
            return verified.Error;
        }

        if (verified.Value.DeviceHash != device.DeviceHash)
        {
            return new Error("license.device", "Bu lisans dosyası başka bir bilgisayar için oluşturulmuş.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SettingsStore.SetAsync(db, SettingKeys.LicenseSigned, signedLicenseJson, now, ct);

            // A license just signed by the server carries trusted time. When this computer's clock agrees with it, a
            // "highest time seen" far ahead of both can only come from a clock that was once set wrong (a year ahead,
            // then fixed) — it would keep a paid license read-only for that year, so it is brought back. A rolled-back
            // clock never qualifies: it cannot agree with a fresh server time.
            var issued = verified.Value.IssuedAtUtc;
            var highWater = await SettingsStore.GetUtcAsync(db, SettingKeys.LicenseHighWaterMarkUtc, ct);
            var trusted = now > issued ? now : issued;
            if (highWater is { } hw && (now - issued).Duration() <= LicenseEvaluator.RollbackTolerance && hw > trusted + LicenseEvaluator.RollbackTolerance)
            {
                await SettingsStore.SetUtcAsync(db, SettingKeys.LicenseHighWaterMarkUtc, trusted, now, ct);
                await audit.AppendAsync(db, "license.clock_corrected", "License", null, $"Saat ileri alınmış görünüyordu ({hw.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}); sunucu saatine göre düzeltildi", ct);
            }

            await audit.AppendAsync(db, action, "License", null, $"Lisans {verified.Value.LicenseId} ({verified.Value.Edition}) {verb}, geçerlilik {verified.Value.ValidUntilUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await RefreshAsync(ct);
    }
}
