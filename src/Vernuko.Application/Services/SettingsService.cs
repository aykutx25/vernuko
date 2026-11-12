using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed record BusinessInfo(
    string BusinessName,
    BusinessProfileId ProfileId,
    string ProfileName,
    string? Phone,
    string? Email,
    string? Address,
    string TimeZoneId,
    string Currency,
    WeeklySchedule BusinessHours,
    int SlotMinutes,
    bool NoShowConsumesSession,
    bool AutoConfirmAppointments,
    int AutoLockMinutes,
    bool IsDemo,
    Terminology Terms,
    IReadOnlySet<ProfileFeature> Features);

public sealed class SettingsService(AppServiceDeps deps) : AppServiceBase(deps)
{
    /// <summary>Basic business info for the shell (name, profile, wording). Available to every signed-in user.</summary>
    public async Task<BusinessInfo?> GetBusinessInfoAsync(CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        var s = await db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null)
        {
            return null;
        }

        var p = s.Profile;
        return new BusinessInfo(s.BusinessName, s.ProfileId, p.DisplayName, s.Phone, s.Email, s.Address, s.TimeZoneId, s.Currency, s.BusinessHours,
            s.SlotMinutes, s.NoShowConsumesSession, s.AutoConfirmAppointments, s.AutoLockMinutes, s.IsDemo, p.Terms, p.Features);
    }

    public Task<Result<bool>> UpdateBusinessAsync(string name, string? phone, string? email, string? address, WeeklySchedule hours, int slotMinutes, bool noShowConsumesSession, bool autoConfirm, int autoLockMinutes, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.SettingsManage, async db =>
        {
            var s = await db.BusinessSettings.FirstAsync(ct);
            if (string.IsNullOrWhiteSpace(name))
            {
                return Error.Validation("settings.name", "İşletme adı zorunludur.");
            }

            s.UpdateContact(name, phone, email, address, s.TaxOffice, s.TaxNumber);
            var h = s.UpdateHours(hours);
            if (h.IsFailure)
            {
                return h.Error;
            }

            s.UpdatePolicies(slotMinutes, noShowConsumesSession, autoConfirm, autoLockMinutes);
            await Audit.AppendAsync(db, "settings.updated", "Business", s.Id, "İşletme ayarları güncellendi", ct);
            return true;
        }, ct);
}
