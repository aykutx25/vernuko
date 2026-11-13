using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Services, staff, rooms/devices, leave/closures and package templates.</summary>
public sealed class CatalogService(AppServiceDeps deps) : AppServiceBase(deps), ICatalogService
{
    public Task<Result<IReadOnlyList<ServiceDto>>> GetServicesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ServiceDto>>(Permission.AppointmentsView, async db =>
        {
            var services = await db.Services.AsNoTracking().Where(s => includeInactive || s.IsActive).OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);
            var links = await db.ServiceResources.AsNoTracking().ToListAsync(ct);
            return services.Select(s => ToDto(s) with { AllowedResourceIds = links.Where(l => l.ServiceId == s.Id).Select(l => l.ResourceId).ToList() }).ToList();
        }, ct);

    public Task<Result<Guid>> SaveServiceAsync(ServiceDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.CatalogManage, async db =>
        {
            var v = ServiceDefinition.ValidateValues(dto.Name, dto.DurationMinutes, dto.PriceMinor, dto.PrepMinutes, dto.CleanupMinutes);
            if (v.IsFailure)
            {
                return v.Error;
            }

            ServiceDefinition? s;
            if (dto.Id == Guid.Empty)
            {
                s = new ServiceDefinition(dto.Name, dto.Category, dto.DurationMinutes, dto.PriceMinor, dto.PrepMinutes, dto.CleanupMinutes, dto.RequiredResourceKind);
                db.Services.Add(s);
            }
            else
            {
                s = await db.Services.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (s is null)
                {
                    return Error.NotFound("Hizmet");
                }

                var r = s.Update(dto.Name, dto.Category, dto.DurationMinutes, dto.PriceMinor, dto.PrepMinutes, dto.CleanupMinutes, dto.RequiredResourceKind);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            var processing = s.SetProcessing(dto.ProcessingStartMinutes, dto.ProcessingMinutes);
            if (processing.IsFailure)
            {
                return processing.Error;
            }

            s.SetActive(dto.IsActive);
            if (dto.AllowedResourceIds is { } allowed)
            {
                var existingLinks = await db.ServiceResources.Where(x => x.ServiceId == s.Id).ToListAsync(ct);
                var ids = allowed.Distinct().ToList();
                var valid = await db.Resources.Where(r => ids.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
                db.ServiceResources.RemoveRange(existingLinks.Where(l => !valid.Contains(l.ResourceId)));
                foreach (var id in valid.Where(id => existingLinks.All(l => l.ResourceId != id)))
                {
                    db.ServiceResources.Add(new ServiceResource(s.Id, id));
                }
            }

            await Audit.AppendAsync(db, "service.saved", "Service", s.Id, $"Hizmet kaydedildi: {s.Name}", ct);
            return s.Id;
        }, ct);

    public Task<Result<IReadOnlyList<StaffDto>>> GetStaffAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<StaffDto>>(Permission.AppointmentsView, async db =>
        {
            var staff = await db.Staff.AsNoTracking().Where(s => includeInactive || s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToListAsync(ct);
            var links = await db.StaffServices.AsNoTracking().ToListAsync(ct);
            return staff.Select(s => new StaffDto(s.Id, s.DisplayName, s.Title, s.Phone, s.Color, s.IsActive, s.WorkingHours,
                links.Where(l => l.StaffMemberId == s.Id).Select(l => l.ServiceId).ToList())).ToList();
        }, ct);

    public Task<Result<Guid>> SaveStaffAsync(StaffDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.StaffManage, async db =>
        {
            if (string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                return Error.Validation("staff.name", "Personel adı zorunludur.");
            }

            StaffMember? s;
            if (dto.Id == Guid.Empty)
            {
                s = new StaffMember(Branch.DefaultId, dto.DisplayName, dto.Title);
                s.SetSortOrder(await db.Staff.CountAsync(ct));
                db.Staff.Add(s);
            }
            else
            {
                s = await db.Staff.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (s is null)
                {
                    return Error.NotFound("Personel");
                }
            }

            s.Update(dto.DisplayName, dto.Title, dto.Phone, dto.Color);
            var hours = s.SetWorkingHours(dto.WorkingHours);
            if (hours.IsFailure)
            {
                return hours.Error;
            }

            s.SetActive(dto.IsActive);
            var existing = await db.StaffServices.Where(x => x.StaffMemberId == s.Id).ToListAsync(ct);
            db.StaffServices.RemoveRange(existing.Where(e => !dto.ServiceIds.Contains(e.ServiceId)));
            foreach (var serviceId in dto.ServiceIds.Distinct().Where(id => existing.All(e => e.ServiceId != id)))
            {
                db.StaffServices.Add(new StaffService(s.Id, serviceId));
            }

            await Audit.AppendAsync(db, "staff.saved", "Staff", s.Id, $"Personel kaydedildi: {s.DisplayName}", ct);
            return s.Id;
        }, ct);

    public Task<Result<IReadOnlyList<ResourceDto>>> GetResourcesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ResourceDto>>(Permission.AppointmentsView, async db =>
            (await db.Resources.AsNoTracking().Where(r => includeInactive || r.IsActive).OrderBy(r => r.Kind).ThenBy(r => r.Name).ToListAsync(ct))
                .Select(r => new ResourceDto(r.Id, r.Name, r.Kind, r.IsActive, r.Notes)).ToList(), ct);

    public Task<Result<Guid>> SaveResourceAsync(ResourceDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.CatalogManage, async db =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Error.Validation("resource.name", "Ad zorunludur.");
            }

            Resource? r;
            if (dto.Id == Guid.Empty)
            {
                r = new Resource(Branch.DefaultId, dto.Name, dto.Kind);
                db.Resources.Add(r);
            }
            else
            {
                r = await db.Resources.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (r is null)
                {
                    return Error.NotFound("Oda/Cihaz");
                }
            }

            r.Update(dto.Name, dto.Kind, dto.Notes);
            r.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, "resource.saved", "Resource", r.Id, $"Kaynak kaydedildi: {r.Name}", ct);
            return r.Id;
        }, ct);

    public Task<Result<IReadOnlyList<ScheduleExceptionDto>>> GetExceptionsAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<ScheduleExceptionDto>>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(to, zone).End;
            var list = await db.ScheduleExceptions.AsNoTracking().Where(e => e.StartUtc < end && e.EndUtc > start).OrderBy(e => e.StartUtc).ToListAsync(ct);
            var staff = await db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
            var resources = await db.Resources.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
            return list.Select(e => new ScheduleExceptionDto(e.Id, e.Scope, e.TargetId,
                e.TargetId is { } t ? staff.GetValueOrDefault(t) ?? resources.GetValueOrDefault(t) : null,
                e.Kind, TimeZones.ToLocal(e.StartUtc, zone), TimeZones.ToLocal(e.EndUtc, zone), e.Note)).ToList();
        }, ct);

    /// <summary>Adds leave / holiday / closure. Existing appointments in that window are not changed automatically.</summary>
    public Task<Result<Guid>> AddExceptionAsync(ScheduleExceptionScope scope, Guid? targetId, ScheduleExceptionKind kind, DateTime startLocal, DateTime endLocal, string? note, CancellationToken ct = default) =>
        WriteAsync<Guid>(scope == ScheduleExceptionScope.Staff ? Permission.StaffManage : Permission.CatalogManage, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            if (endLocal <= startLocal)
            {
                return Error.Validation("exception.range", "Bitiş, başlangıçtan sonra olmalıdır.");
            }

            var e = new ScheduleException(scope, targetId, kind, TimeZones.ToUtc(startLocal, zone), TimeZones.ToUtc(endLocal, zone), note);
            db.ScheduleExceptions.Add(e);
            await Audit.AppendAsync(db, "schedule_exception.added", "ScheduleException", e.Id, $"{ScheduleException.KindName(kind)} eklendi", ct);
            return e.Id;
        }, ct);

    /// <summary>Needs the same permission as adding it: staff leave = StaffManage, closures/devices = CatalogManage.</summary>
    public async Task<Result<bool>> RemoveExceptionAsync(Guid id, CancellationToken ct = default)
    {
        ScheduleExceptionScope? scope;
        await using (var lookup = await Factory.CreateDbContextAsync(ct))
        {
            scope = await lookup.ScheduleExceptions.AsNoTracking().Where(x => x.Id == id).Select(x => (ScheduleExceptionScope?)x.Scope).FirstOrDefaultAsync(ct);
        }

        return await WriteAsync<bool>(scope == ScheduleExceptionScope.Staff ? Permission.StaffManage : Permission.CatalogManage, async db =>
        {
            var e = await db.ScheduleExceptions.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (e is null)
            {
                return Error.NotFound("Kayıt");
            }

            db.ScheduleExceptions.Remove(e);
            await Audit.AppendAsync(db, "schedule_exception.removed", "ScheduleException", e.Id, $"{ScheduleException.KindName(e.Kind)} kaldırıldı", ct);
            return true;
        }, ct);
    }

    public Task<Result<IReadOnlyList<PackageTemplateDto>>> GetPackageTemplatesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<PackageTemplateDto>>(Permission.PackagesView, async db =>
        {
            var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            return (await db.PackageTemplates.AsNoTracking().Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync(ct))
                .Select(p => new PackageTemplateDto(p.Id, p.Name, p.ServiceId, services.GetValueOrDefault(p.ServiceId) ?? "?", p.SessionCount, p.PriceMinor, p.ValidityDays, p.IsActive))
                .ToList();
        }, ct);

    public Task<Result<Guid>> SavePackageTemplateAsync(PackageTemplateDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.CatalogManage, async db =>
        {
            if (!await db.Services.AnyAsync(s => s.Id == dto.ServiceId, ct))
            {
                return Error.Validation("package.service", "Hizmet seçilmelidir.");
            }

            PackageTemplate? t;
            if (dto.Id == Guid.Empty)
            {
                var probe = PackageTemplateValidate(dto);
                if (probe.IsFailure)
                {
                    return probe.Error;
                }

                t = new PackageTemplate(dto.Name, dto.ServiceId, dto.SessionCount, dto.PriceMinor, dto.ValidityDays);
                db.PackageTemplates.Add(t);
            }
            else
            {
                t = await db.PackageTemplates.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
                if (t is null)
                {
                    return Error.NotFound("Paket tanımı");
                }

                var r = t.Update(dto.Name, dto.ServiceId, dto.SessionCount, dto.PriceMinor, dto.ValidityDays);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            t.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, "package_template.saved", "PackageTemplate", t.Id, $"Paket tanımı kaydedildi: {t.Name}", ct);
            return t.Id;
        }, ct);

    private static Result PackageTemplateValidate(PackageTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Error.Validation("package.name", "Paket adı zorunludur.");
        }

        if (dto.SessionCount is < 1 or > 500)
        {
            return Error.Validation("package.sessions", "Seans sayısı 1 ile 500 arasında olmalıdır.");
        }

        if (dto.PriceMinor < 0)
        {
            return Error.Validation("package.price", "Paket fiyatı negatif olamaz.");
        }

        return dto.ValidityDays is < 1 or > 3650 ? Error.Validation("package.validity", "Geçerlilik süresi 1 ile 3650 gün arasında olmalıdır.") : Result.Success();
    }

    private static ServiceDto ToDto(ServiceDefinition s) =>
        new(s.Id, s.Name, s.Category, s.DurationMinutes, s.PrepMinutes, s.CleanupMinutes, s.PriceMinor, s.RequiredResourceKind, s.IsActive, null, s.ProcessingStartMinutes, s.ProcessingMinutes);
}
