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
}

/// <summary>
/// Evaluates the license locally (no network needed). Expiry switches the app to read-only mode;
/// it never deletes or locks customer data.
/// </summary>
public sealed class LicenseService(IDbContextFactory<VernukoDbContext> factory, TimeProvider time, IDeviceIdentity device, LicensingOptions options, UserSession session, AuditWriter audit) : ILicenseGate
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
            await audit.AppendAsync(db, "license.imported", "License", null, $"Lisans {verified.Value.LicenseId} ({verified.Value.Edition}) yüklendi, geçerlilik {verified.Value.ValidUntilUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await RefreshAsync(ct);
    }
}
