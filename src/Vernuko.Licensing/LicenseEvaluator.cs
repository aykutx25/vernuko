namespace Vernuko.Licensing;

public enum LicenseState
{
    Trial,
    Active,

    /// <summary>Refresh is overdue but the offline grace window has not ended.</summary>
    OfflineGrace,

    TrialExpired,
    Expired,

    /// <summary>Signed for another device; needs (re)activation.</summary>
    DeviceMismatch,
    Invalid,
}

public enum AccessMode
{
    Full,

    /// <summary>
    /// Data stays fully readable, exportable and backed up; only creating/changing business records is blocked.
    /// Customer data is NEVER deleted or locked because of licensing.
    /// </summary>
    ReadOnly,
}

public sealed record LicenseStatus(LicenseState State, AccessMode Mode, DateTime? ValidUntilUtc, int? DaysLeft, bool ClockRollbackSuspected, string Message)
{
    public bool CanWrite => Mode == AccessMode.Full;
}

/// <summary>Local, persisted license bookkeeping (stored in the database).</summary>
public sealed record LicenseLocalState(DateTime TrialStartedUtc, DateTime HighWaterMarkUtc, string? SignedLicense);

/// <summary>
/// Pure evaluation of the license state. Clock-rollback protection: the effective time is
/// max(system clock, highest time ever observed), so turning the clock back cannot extend a license — but it
/// also never locks a paying customer out by itself (no false-positive lockouts).
/// </summary>
public static class LicenseEvaluator
{
    public const int TrialDays = 30;

    /// <summary>Clock differences below this are treated as normal drift / time-zone fixes.</summary>
    public static readonly TimeSpan RollbackTolerance = TimeSpan.FromHours(48);

    public static LicenseStatus Evaluate(LicenseLocalState local, LicenseDocument? license, string deviceHash, DateTime systemNowUtc, bool licenseSignatureValid = true)
    {
        var rollback = systemNowUtc < local.HighWaterMarkUtc - RollbackTolerance;
        var now = systemNowUtc > local.HighWaterMarkUtc ? systemNowUtc : local.HighWaterMarkUtc;

        if (local.SignedLicense is not null && !licenseSignatureValid)
        {
            return new LicenseStatus(LicenseState.Invalid, AccessMode.ReadOnly, null, null, rollback,
                "Lisans dosyası doğrulanamadı. Verileriniz güvende; lisansınızı yeniden etkinleştirin.");
        }

        if (license is null)
        {
            var trialEnd = local.TrialStartedUtc.AddDays(TrialDays);
            if (now < trialEnd)
            {
                var days = (int)Math.Ceiling((trialEnd - now).TotalDays);
                return new LicenseStatus(LicenseState.Trial, AccessMode.Full, trialEnd, days, rollback, $"Deneme sürümü: {days} gün kaldı.");
            }

            return new LicenseStatus(LicenseState.TrialExpired, AccessMode.ReadOnly, trialEnd, 0, rollback,
                "Deneme süresi doldu. Kayıtlarınızı görüntüleyebilir, yedek alabilir ve dışa aktarabilirsiniz.");
        }

        if (!string.Equals(license.DeviceHash, deviceHash, StringComparison.Ordinal))
        {
            return new LicenseStatus(LicenseState.DeviceMismatch, AccessMode.ReadOnly, license.ValidUntilUtc, null, rollback,
                "Bu lisans başka bir bilgisayar için etkinleştirilmiş. Lisansı bu cihaza taşımak için yeniden etkinleştirin.");
        }

        if (now >= license.ValidUntilUtc)
        {
            return new LicenseStatus(LicenseState.Expired, AccessMode.ReadOnly, license.ValidUntilUtc, 0, rollback,
                "Abonelik süresi doldu. Verileriniz silinmedi; yenileme sonrası kaldığınız yerden devam edebilirsiniz.");
        }

        var graceEnd = license.RefreshAfterUtc.AddDays(license.OfflineGraceDays);
        if (now >= graceEnd)
        {
            return new LicenseStatus(LicenseState.Expired, AccessMode.ReadOnly, license.ValidUntilUtc, 0, rollback,
                "Lisans uzun süredir doğrulanamadı. İnternete bağlanarak lisansı yenileyin.");
        }

        var daysLeft = (int)Math.Ceiling((license.ValidUntilUtc - now).TotalDays);
        if (now >= license.RefreshAfterUtc)
        {
            var graceDays = (int)Math.Ceiling((graceEnd - now).TotalDays);
            return new LicenseStatus(LicenseState.OfflineGrace, AccessMode.Full, license.ValidUntilUtc, daysLeft, rollback,
                $"Lisans {graceDays} gün içinde çevrim içi doğrulanmalı.");
        }

        return new LicenseStatus(LicenseState.Active, AccessMode.Full, license.ValidUntilUtc, daysLeft, rollback, $"Lisans aktif ({license.Edition}).");
    }

    public static LicenseLocalState AdvanceHighWaterMark(LicenseLocalState local, DateTime systemNowUtc) =>
        systemNowUtc > local.HighWaterMarkUtc ? local with { HighWaterMarkUtc = systemNowUtc } : local;
}
