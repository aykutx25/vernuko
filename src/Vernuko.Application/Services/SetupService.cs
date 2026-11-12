using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>First-run wizard: creates the business, staff, services, admin account and (optionally) backup keys atomically.</summary>
public sealed class SetupService(IDbContextFactory<VernukoDbContext> factory, PasswordHasher hasher, TimeProvider time, BackupKeyStore keys, AuditWriter audit)
{
    public async Task<bool> IsCompletedAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.BusinessSettings.AnyAsync(s => s.SetupCompletedAtUtc != null, ct);
    }

    public static Result Validate(SetupRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.BusinessName))
        {
            return Error.Validation("setup.business_name", "İşletme adı zorunludur.");
        }

        var hours = r.BusinessHours.Validate();
        if (hours.IsFailure)
        {
            return hours;
        }

        if (!r.BusinessHours.Days.Values.Any(d => d.Count > 0))
        {
            return Error.Validation("setup.no_open_days", "En az bir çalışma günü seçilmelidir.");
        }

        if (r.Staff.Count == 0 || r.Staff.Any(s => string.IsNullOrWhiteSpace(s.DisplayName)))
        {
            return Error.Validation("setup.staff", "En az bir personel adı girilmelidir.");
        }

        foreach (var s in r.Services)
        {
            var v = ServiceDefinition.ValidateValues(s.Name, s.DurationMinutes, s.PriceMinor, s.PrepMinutes, s.CleanupMinutes);
            if (v.IsFailure)
            {
                return v;
            }
        }

        if (string.IsNullOrWhiteSpace(r.AdminDisplayName) || string.IsNullOrWhiteSpace(r.AdminUsername))
        {
            return Error.Validation("setup.admin", "Yönetici adı ve kullanıcı adı zorunludur.");
        }

        if (r.AdminUsername.Trim().Length < 3 || r.AdminUsername.Any(char.IsWhiteSpace))
        {
            return Error.Validation("setup.username", "Kullanıcı adı en az 3 karakter olmalı ve boşluk içermemelidir.");
        }

        var pw = PasswordPolicy.Validate(r.AdminPassword, r.AdminUsername);
        if (pw.IsFailure)
        {
            return pw;
        }

        _ = TimeZones.Find(r.TimeZoneId);
        return Result.Success();
    }

    public async Task<Result<SetupResult>> CompleteAsync(SetupRequest r, CancellationToken ct = default)
    {
        var validation = Validate(r);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var now = time.GetUtcNow().UtcDateTime;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.BusinessSettings.AnyAsync(ct))
        {
            return AppErrors.SetupAlreadyCompleted;
        }

        var profile = BusinessProfileCatalog.Get(r.ProfileId);
        db.Branches.Add(new Branch(Branch.DefaultId, r.BusinessName));
        var settings = new BusinessSettings(r.BusinessName, r.ProfileId, r.TimeZoneId, r.BusinessHours);
        settings.UpdateContact(r.BusinessName, r.Phone, null, r.Address, null, null);
        settings.MarkSetupCompleted(now, r.IsDemo);
        db.BusinessSettings.Add(settings);

        var order = 0;
        foreach (var s in r.Staff)
        {
            var staff = new StaffMember(Branch.DefaultId, s.DisplayName, s.Title);
            staff.SetSortOrder(order++);
            db.Staff.Add(staff);
        }

        var services = new List<ServiceDefinition>();
        foreach (var s in r.Services)
        {
            var service = new ServiceDefinition(s.Name, s.Category, s.DurationMinutes, s.PriceMinor, s.PrepMinutes, s.CleanupMinutes, s.RequiredResourceKind);
            services.Add(service);
            db.Services.Add(service);
        }

        foreach (var res in r.Resources.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            db.Resources.Add(new Resource(Branch.DefaultId, res.Name, res.Kind));
        }

        foreach (var suggestion in profile.SuggestedPackages)
        {
            var service = services.FirstOrDefault(s => s.Name == suggestion.ServiceName);
            if (service is not null)
            {
                db.PackageTemplates.Add(new PackageTemplate(suggestion.Name, service.Id, suggestion.Sessions, service.PriceMinor * suggestion.Sessions, suggestion.ValidityDays));
            }
        }

        var admin = new UserAccount(r.AdminUsername, r.AdminDisplayName, hasher.Hash(r.AdminPassword), SystemRole.Owner);
        db.Users.Add(admin);

        await SettingsStore.SetAsync(db, SettingKeys.InstallationId, Ids.New().ToString(), now, ct);
        await SettingsStore.SetUtcAsync(db, SettingKeys.LicenseTrialStartedUtc, now, now, ct);
        await SettingsStore.SetUtcAsync(db, SettingKeys.LicenseHighWaterMarkUtc, now, now, ct);

        string? recoveryKey = null;
        if (r.EnableLocalBackup)
        {
            recoveryKey = await keys.InitializeAsync(db, now, ct);
            if (!string.IsNullOrWhiteSpace(r.BackupFolder))
            {
                await SettingsStore.SetAsync(db, SettingKeys.BackupFolder, r.BackupFolder.Trim(), now, ct);
            }

            await SettingsStore.SetAsync(db, SettingKeys.BackupSchedule, "Daily", now, ct);
            await SettingsStore.SetAsync(db, SettingKeys.BackupOnExit, "true", now, ct);
        }

        await audit.AppendAsAsync(db, admin.Id, admin.Username, "setup.completed", "Business", settings.Id, $"Kurulum tamamlandı: profil {r.ProfileId}, {r.Staff.Count} personel, {r.Services.Count} hizmet", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new SetupResult(admin.Id, recoveryKey);
    }
}
