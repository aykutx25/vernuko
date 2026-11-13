using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Reporting;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>The shared appointment engine used by every business profile.</summary>
public sealed class AppointmentService(AppServiceDeps deps) : AppServiceBase(deps), IAppointmentService
{
    public Task<Result<DayAgenda>> GetDayAsync(DateOnly day, AppointmentFilter filter, CancellationToken ct = default) =>
        ReadAsync<DayAgenda>(Permission.AppointmentsView, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var range = TimeZones.DayRange(day, zone);
            var ids = await Filtered(db, filter).Where(a => a.StartUtc >= range.Start && a.StartUtc < range.End).Select(a => a.Id).ToListAsync(ct);
            var items = await AppointmentQueries.LoadItemsAsync(db, ids, zone, ct);
            var hours = settings.BusinessHours.For(day.DayOfWeek).OrderBy(i => i.Start).ToList();
            var breaks = hours.Zip(hours.Skip(1), (a, b) => new TimeInterval(a.End, b.Start)).Where(i => i.IsValid).ToList();
            var activeStaff = await db.Staff.CountAsync(s => s.IsActive, ct);
            return new DayAgenda(day, items, DailySummary.From(items.Select(i => i.Status)), activeStaff, breaks, hours.Count > 0);
        }, ct);

    public Task<Result<IReadOnlyList<AppointmentListItem>>> GetRangeAsync(DateOnly from, DateOnly toInclusive, AppointmentFilter filter, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<AppointmentListItem>>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(toInclusive, zone).End;
            var ids = await Filtered(db, filter).Where(a => a.StartUtc >= start && a.StartUtc < end).Select(a => a.Id).ToListAsync(ct);
            return Result.Success(await AppointmentQueries.LoadItemsAsync(db, ids, zone, ct));
        }, ct);

    public Task<Result<IReadOnlyList<AppointmentListItem>>> GetUpcomingAsync(int take, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<AppointmentListItem>>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var now = UtcNow;
            var open = new[] { AppointmentStatus.Created, AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Waiting };
            var ids = await db.Appointments.AsNoTracking().Where(a => a.StartUtc >= now && open.Contains(a.Status))
                .OrderBy(a => a.StartUtc).Take(Math.Clamp(take, 1, 100)).Select(a => a.Id).ToListAsync(ct);
            return Result.Success(await AppointmentQueries.LoadItemsAsync(db, ids, zone, ct));
        }, ct);

    public Task<Result<AppointmentListItem>> GetAsync(Guid id, CancellationToken ct = default) =>
        ReadAsync<AppointmentListItem>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var items = await AppointmentQueries.LoadItemsAsync(db, [id], zone, ct);
            return items.Count == 0 ? Error.NotFound("Randevu") : items[0];
        }, ct);

    public Task<Result<IReadOnlyList<SlotSuggestion>>> GetAvailableSlotsAsync(SlotQuery q, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<SlotSuggestion>>(Permission.AppointmentsView, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == q.ServiceId, ct);
            if (service is null)
            {
                return Error.NotFound("Hizmet");
            }

            IReadOnlyList<IReadOnlyList<Guid>> combinations = [q.ResourceIds];
            if (q.SearchAllowedResources)
            {
                var allowedIds = await db.ServiceResources.AsNoTracking().Where(x => x.ServiceId == service.Id).Select(x => x.ResourceId).ToListAsync(ct);
                if (allowedIds.Count > 0)
                {
                    var allowed = await db.Resources.AsNoTracking().Where(x => allowedIds.Contains(x.Id) && x.IsActive).OrderBy(x => x.Name).Select(x => new ResourceRef(x.Id, x.Kind)).ToListAsync(ct);
                    var selectedIds = q.ResourceIds.ToList();
                    var selected = await db.Resources.AsNoTracking().Where(x => selectedIds.Contains(x.Id)).Select(x => new ResourceRef(x.Id, x.Kind)).ToListAsync(ct);
                    combinations = AllowedCombinations(selected, allowed);
                }
            }

            var found = new Dictionary<TimeOnly, SlotSuggestion>();
            foreach (var resourceIds in combinations)
            {
                var ctx = await AppointmentQueries.LoadDayContextAsync(db, settings, zone, q.Day, q.StaffMemberId, resourceIds, ct);
                if (q.IgnoreAppointmentId is { } ignore)
                {
                    ctx = ctx with { Bookings = ctx.Bookings.Where(b => b.AppointmentId != ignore).ToList() };
                }

                var template = new AppointmentSlotTemplate(q.DurationMinutesOverride ?? service.DurationMinutes, service.PrepMinutes, service.CleanupMinutes, q.StaffMemberId, resourceIds,
                    service.ProcessingStartMinutes, service.ProcessingMinutes);
                foreach (var s in AvailabilityCalculator.FindSlots(ctx, template, settings.SlotMinutes, UtcNow))
                {
                    found.TryAdd(s.LocalStart, new SlotSuggestion(s.LocalStart, s.StartUtc, resourceIds));
                }
            }

            return found.Values.OrderBy(s => s.LocalStart).ToList();
        }, ct);

    private sealed record ResourceRef(Guid Id, ResourceKind Kind);

    /// <summary>
    /// Every way to pick one allowed resource of each linked kind (e.g. 2 laser rooms × 1 device = 2), the current
    /// selection first. Selected resources of other kinds are kept. Capped so a large catalogue stays fast.
    /// </summary>
    private static List<IReadOnlyList<Guid>> AllowedCombinations(IReadOnlyList<ResourceRef> selected, IReadOnlyList<ResourceRef> allowed)
    {
        const int Max = 24;
        var kinds = allowed.Select(a => a.Kind).Distinct().ToList();
        var keep = selected.Where(s => !kinds.Contains(s.Kind)).Select(s => s.Id).ToList();
        IEnumerable<List<Guid>> combos = [keep];
        foreach (var kind in kinds)
        {
            // Selected allowed resources of this kind are tried before the others.
            var options = allowed.Where(a => a.Kind == kind).OrderByDescending(a => selected.Any(s => s.Id == a.Id)).Select(a => a.Id).ToList();
            combos = combos.SelectMany(c => options.Select(o => c.Append(o).ToList())).ToList();
        }

        return combos.Take(Max).Select(c => (IReadOnlyList<Guid>)c).ToList();
    }

    /// <summary>
    /// Books an appointment inside a write-locked transaction (check + insert are atomic, also across processes).
    /// Blocking issues prevent saving; warnings require <paramref name="confirmWarnings"/>.
    /// </summary>
    public Task<Result<BookingOutcome>> CreateAsync(CreateAppointmentRequest r, bool confirmWarnings, CancellationToken ct = default) =>
        WriteAsync<BookingOutcome>(Permission.AppointmentsManage, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("appointment.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.ServiceId, ct);
            if (service is null || !service.IsActive)
            {
                return Error.Validation("appointment.service", "Hizmet bulunamadı veya pasif.");
            }

            var prepared = await PrepareSlotAsync(db, settings, zone, service, r.StaffMemberId, r.ResourceIds, r.StartLocal, r.DurationMinutesOverride, null, ct);
            if (prepared.IsFailure)
            {
                return prepared.Error;
            }

            var (slot, issues) = prepared.Value;
            long price = service.PriceMinor;
            if (r.CustomerPackageId is { } packageId)
            {
                var package = await db.CustomerPackages.AsNoTracking().Include(p => p.Entries).FirstOrDefaultAsync(p => p.Id == packageId, ct);
                if (package is null || package.CustomerId != r.CustomerId)
                {
                    return Error.Validation("appointment.package", "Seçilen paket bu müşteriye ait değil.");
                }

                if (package.ServiceId != r.ServiceId)
                {
                    issues.Add(new SchedulingIssue("package.service", "Seçilen paket bu hizmet için tanımlanmamış.", IssueSeverity.Blocking));
                }

                var canBook = package.CanBook(UtcNow);
                if (canBook.IsFailure)
                {
                    issues.Add(new SchedulingIssue(canBook.Error.Code, canBook.Error.Message, IssueSeverity.Blocking));
                }
                else
                {
                    var upcoming = await CountOpenPackageAppointmentsAsync(db, packageId, null, ct);
                    if (upcoming >= package.RemainingSessions)
                    {
                        issues.Add(new SchedulingIssue("package.overbooked", $"Paketin kalan {package.RemainingSessions} seansı için zaten {upcoming} açık randevu var.", IssueSeverity.RequiresConfirmation));
                    }
                }

                price = 0; // already charged when the package was sold
            }

            if (issues.Any(i => i.Severity == IssueSeverity.Blocking) || (issues.Count > 0 && !confirmWarnings))
            {
                return new BookingOutcome(null, issues);
            }

            var created = Appointment.Create(
                Branch.DefaultId, r.CustomerId, r.ServiceId, slot, price,
                settings.AutoConfirmAppointments ? AppointmentStatus.Confirmed : AppointmentStatus.Created,
                r.Source, r.Notes, r.BodyArea, r.CustomerPackageId, UserId, UtcNow);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var meeting = created.Value.SetMeetingUrl(r.MeetingUrl);
            if (meeting.IsFailure)
            {
                return meeting.Error;
            }

            db.Appointments.Add(created.Value);
            await Audit.AppendAsync(db, "appointment.created", "Appointment", created.Value.Id,
                $"Randevu oluşturuldu: müşteri #{customer.Number}, {TimeZones.ToLocal(slot.StartUtc, zone):yyyy-MM-dd HH:mm}" + (issues.Count > 0 ? $", onaylanan uyarılar: {string.Join(",", issues.Select(i => i.Code))}" : string.Empty), ct);
            return new BookingOutcome(created.Value.Id, issues);
        }, ct);

    public Task<Result<BookingOutcome>> RescheduleAsync(RescheduleRequest r, bool confirmWarnings, CancellationToken ct = default) =>
        WriteAsync<BookingOutcome>(Permission.AppointmentsManage, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var appt = await db.Appointments.Include(a => a.Bookings).Include(a => a.History).FirstOrDefaultAsync(a => a.Id == r.AppointmentId, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            if (!AppointmentStatusRules.CanReschedule(appt.Status))
            {
                return Error.Validation("appointment.reschedule_status", $"\"{AppointmentStatusRules.DisplayName(appt.Status)}\" durumundaki randevu yeniden planlanamaz.");
            }

            var service = await db.Services.AsNoTracking().FirstAsync(s => s.Id == appt.ServiceId, ct);
            var oldStart = TimeZones.ToLocal(appt.StartUtc, zone);
            var shift = r.NewStartLocal - oldStart;

            // The appointment itself plus (optionally) every later open appointment of the same series.
            var group = new List<Appointment> { appt };
            if (r.ApplyToFollowing && appt.SeriesId is { } seriesId)
            {
                var following = await db.Appointments.Include(a => a.Bookings).Include(a => a.History)
                    .Where(a => a.SeriesId == seriesId && a.Id != appt.Id && a.StartUtc > appt.StartUtc)
                    .OrderBy(a => a.StartUtc)
                    .ToListAsync(ct);
                group.AddRange(following.Where(a => AppointmentStatusRules.CanReschedule(a.Status)));
            }

            var ignore = group.Select(a => a.Id).ToHashSet();
            var plan = new List<(Appointment Appointment, AppointmentSlot Slot)>();
            var allIssues = new List<SchedulingIssue>();
            foreach (var a in group)
            {
                var current = a.CurrentSlot();
                var newLocal = a == appt ? r.NewStartLocal : TimeZones.ToLocal(a.StartUtc, zone) + shift;
                var prepared = await PrepareSlotAsync(db, settings, zone, service, r.StaffMemberId ?? current.StaffMemberId, r.ResourceIds ?? current.ResourceIds,
                    newLocal, r.DurationMinutes ?? current.DurationMinutes, a.Id, ct, ignore);
                if (prepared.IsFailure)
                {
                    return prepared.Error;
                }

                var (slot, issues) = prepared.Value;
                var prefix = group.Count > 1 ? $"{newLocal:dd.MM.yyyy HH:mm}: " : string.Empty;
                allIssues.AddRange(issues.Select(i => i with { Message = prefix + i.Message }));
                plan.Add((a, slot));
            }

            if (allIssues.Any(i => i.Severity == IssueSeverity.Blocking) || (allIssues.Count > 0 && !confirmWarnings))
            {
                return new BookingOutcome(null, allIssues);
            }

            if (plan.Count > 1)
            {
                foreach (var (a, _) in plan)
                {
                    a.SuspendBookings();
                }

                await db.SaveChangesAsync(ct);
            }

            foreach (var (a, slot) in plan)
            {
                var res = a.Reschedule(slot, UtcNow, UserId);
                if (res.IsFailure)
                {
                    return res.Error;
                }
            }

            await Audit.AppendAsync(db, "appointment.rescheduled", "Appointment", appt.Id,
                $"Randevu {oldStart:yyyy-MM-dd HH:mm} → {r.NewStartLocal:yyyy-MM-dd HH:mm}" + (plan.Count > 1 ? $" (serinin {plan.Count - 1} sonraki randevusu da taşındı)" : string.Empty), ct);
            return new BookingOutcome(appt.Id, allIssues);
        }, ct);

    /// <summary>
    /// Books a recurring series ("her hafta, 8 kez"). Every occurrence is validated like a single booking. Dates that
    /// cannot be booked are reported; with <paramref name="skipBlocked"/> the remaining ones are booked anyway.
    /// </summary>
    public Task<Result<SeriesOutcome>> CreateSeriesAsync(CreateAppointmentRequest r, RecurrencePattern pattern, bool confirmWarnings, bool skipBlocked, CancellationToken ct = default) =>
        WriteAsync<SeriesOutcome>(Permission.AppointmentsManage, async db =>
        {
            var valid = pattern.Validate();
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("appointment.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.ServiceId, ct);
            if (service is null || !service.IsActive)
            {
                return Error.Validation("appointment.service", "Hizmet bulunamadı veya pasif.");
            }

            var seriesIssues = new List<SchedulingIssue>();
            long price = service.PriceMinor;
            if (r.CustomerPackageId is { } packageId)
            {
                var package = await db.CustomerPackages.AsNoTracking().Include(p => p.Entries).FirstOrDefaultAsync(p => p.Id == packageId, ct);
                if (package is null || package.CustomerId != r.CustomerId || package.ServiceId != r.ServiceId)
                {
                    return Error.Validation("appointment.package", "Seçilen paket bu müşteri ve hizmet için geçerli değil.");
                }

                var canBook = package.CanBook(UtcNow);
                if (canBook.IsFailure)
                {
                    return canBook.Error;
                }

                var upcoming = await CountOpenPackageAppointmentsAsync(db, packageId, null, ct);
                if (upcoming + pattern.Count > package.RemainingSessions)
                {
                    seriesIssues.Add(new SchedulingIssue("package.overbooked", $"Paketin kalan {package.RemainingSessions} seansı var; bu seriyle {upcoming + pattern.Count} açık randevu olacak.", IssueSeverity.RequiresConfirmation));
                }

                price = 0;
            }

            var problems = new List<(DateTime StartLocal, IReadOnlyList<SchedulingIssue> Issues)>();
            var plan = new List<(DateTime Local, AppointmentSlot Slot)>();
            foreach (var local in pattern.Occurrences(r.StartLocal))
            {
                var prepared = await PrepareSlotAsync(db, settings, zone, service, r.StaffMemberId, r.ResourceIds, local, r.DurationMinutesOverride, null, ct);
                if (prepared.IsFailure)
                {
                    return prepared.Error;
                }

                var (slot, issues) = prepared.Value;
                if (issues.Count > 0)
                {
                    problems.Add((local, issues));
                }

                if (!issues.Any(i => i.Severity == IssueSeverity.Blocking))
                {
                    plan.Add((local, slot));
                }
            }

            if (seriesIssues.Count > 0)
            {
                problems.Insert(0, (r.StartLocal, seriesIssues));
            }

            var hasBlocking = problems.Any(p => p.Issues.Any(i => i.Severity == IssueSeverity.Blocking));
            var hasWarnings = problems.Any(p => p.Issues.Any(i => i.Severity == IssueSeverity.RequiresConfirmation));
            if ((hasBlocking && !skipBlocked) || (hasWarnings && !confirmWarnings))
            {
                return new SeriesOutcome(null, [], problems);
            }

            if (plan.Count == 0)
            {
                return Error.Validation("series.empty", "Serideki tarihlerin hiçbirine randevu verilemiyor.");
            }

            var seriesId = Ids.New();
            foreach (var (_, slot) in plan)
            {
                var created = Appointment.Create(Branch.DefaultId, r.CustomerId, r.ServiceId, slot, price,
                    settings.AutoConfirmAppointments ? AppointmentStatus.Confirmed : AppointmentStatus.Created,
                    r.Source, r.Notes, r.BodyArea, r.CustomerPackageId, UserId, UtcNow, seriesId);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                var meeting = created.Value.SetMeetingUrl(r.MeetingUrl);
                if (meeting.IsFailure)
                {
                    return meeting.Error;
                }

                db.Appointments.Add(created.Value);
            }

            await Audit.AppendAsync(db, "appointment.series_created", "AppointmentSeries", seriesId,
                $"Tekrarlayan randevu: müşteri #{customer.Number}, {pattern.Describe()}, {plan.Count} randevu oluşturuldu" + (problems.Count > 0 ? $", {problems.Count} tarihte uyarı/çakışma" : string.Empty), ct);
            return new SeriesOutcome(seriesId, plan.Select(p => p.Local).ToList(), problems);
        }, ct);

    /// <summary>Cancels this appointment and every later open appointment of its series.</summary>
    public Task<Result<int>> CancelSeriesFromAsync(Guid appointmentId, string reason, CancellationToken ct = default) =>
        WriteAsync<int>(Permission.AppointmentsManage, async db =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Error.Validation("appointment.reason", "İptal nedeni zorunludur.");
            }

            var appt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
            if (appt?.SeriesId is not { } seriesId)
            {
                return Error.Validation("series.none", "Bu randevu bir serinin parçası değil.");
            }

            var targets = await db.Appointments.Include(a => a.Bookings).Include(a => a.History)
                .Where(a => a.SeriesId == seriesId && a.StartUtc >= appt.StartUtc)
                .ToListAsync(ct);
            var count = 0;
            foreach (var a in targets.Where(a => AppointmentStatusRules.CanTransition(a.Status, AppointmentStatus.Cancelled)))
            {
                if (a.ChangeStatus(AppointmentStatus.Cancelled, UtcNow, UserId, reason).IsSuccess)
                {
                    count++;
                }
            }

            await Audit.AppendAsync(db, "appointment.series_cancelled", "AppointmentSeries", seriesId, $"Serinin {count} randevusu iptal edildi", ct);
            return count;
        }, ct);

    /// <summary>
    /// Changes the status and applies side effects atomically: package session consumption/restoration and service
    /// charges. Cancelling or marking no-show never consumes a session unless the business enabled that policy.
    /// </summary>
    public Task<Result<AppointmentStatus>> ChangeStatusAsync(Guid id, AppointmentStatus to, string? reason, CancellationToken ct = default) =>
        WriteAsync<AppointmentStatus>(Permission.AppointmentsChangeStatus, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var appt = await db.Appointments.Include(a => a.Bookings).Include(a => a.History).FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            var from = appt.Status;
            var reopening = from == AppointmentStatus.Completed && to == AppointmentStatus.InProgress;
            if (reopening && !Session.Has(Permission.AppointmentsManage))
            {
                return Error.Forbidden;
            }

            if (Appointment.WillActivateBookings(from, to))
            {
                var slot = appt.CurrentSlot();
                var ctx = await AppointmentQueries.LoadDayContextAsync(db, settings, zone, TimeZones.LocalDate(slot.StartUtc, zone), slot.StaffMemberId, slot.ResourceIds, ct);
                var conflicts = ConflictDetector.Find(slot, ctx.Bookings, appt.Id);
                if (conflicts.Count > 0)
                {
                    return Error.Conflict("reactivate", "Bu randevunun saati artık başka bir randevuyla dolu. Randevuyu yeni bir saate taşıyın.");
                }
            }

            var changed = appt.ChangeStatus(to, UtcNow, UserId, reason);
            if (changed.IsFailure)
            {
                return changed.Error;
            }

            var sideEffect = await ApplySideEffectsAsync(db, settings, appt, from, to, reason, ct);
            if (sideEffect.IsFailure)
            {
                return sideEffect.Error;
            }

            await Audit.AppendAsync(db, "appointment.status", "Appointment", appt.Id,
                $"{AppointmentStatusRules.DisplayName(from)} → {AppointmentStatusRules.DisplayName(to)}" + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " (açıklama girildi)"), ct);
            return to;
        }, ct);

    public Task<Result<bool>> UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.AppointmentsManage, async db =>
        {
            var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            if (notes is { Length: > Appointment.MaxNotesLength })
            {
                return Error.Validation("appointment.notes", $"Not en fazla {Appointment.MaxNotesLength} karakter olabilir.");
            }

            appt.UpdateNotes(notes);
            await Audit.AppendAsync(db, "appointment.notes", "Appointment", appt.Id, "Randevu notu güncellendi", ct);
            return true;
        }, ct);

    /// <summary>Adds, changes or removes (null) the online meeting link of an appointment.</summary>
    public Task<Result<bool>> SetMeetingUrlAsync(Guid id, string? url, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.AppointmentsManage, async db =>
        {
            var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            var r = appt.SetMeetingUrl(url);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "appointment.meeting_url", "Appointment", appt.Id, appt.MeetingUrl is null ? "Görüşme bağlantısı kaldırıldı" : "Görüşme bağlantısı kaydedildi", ct);
            return true;
        }, ct);

    private async Task<Result> ApplySideEffectsAsync(VernukoDbContext db, BusinessSettings settings, Appointment appt, AppointmentStatus from, AppointmentStatus to, string? reason, CancellationToken ct)
    {
        CustomerPackage? package = null;
        if (appt.CustomerPackageId is { } pid)
        {
            package = await db.CustomerPackages.Include(p => p.Entries).FirstAsync(p => p.Id == pid, ct);
        }

        var account = await ApplyAccountEffectsAsync(db, settings, appt, from, to, reason, package, ct);
        if (account.IsFailure)
        {
            return account;
        }

        // Staff commission and automatic material consumption follow the same complete / re-open lifecycle.
        if (to == AppointmentStatus.Completed)
        {
            await OperationsPosting.OnAppointmentCompletedAsync(db, appt, package, UserId, UtcNow, ct);
        }
        else if (to == AppointmentStatus.InProgress && from == AppointmentStatus.Completed)
        {
            await OperationsPosting.OnAppointmentReopenedAsync(db, appt.Id, UserId, UtcNow, reason ?? "Tamamlama geri alındı", ct);
        }

        return Result.Success();
    }

    private async Task<Result> ApplyAccountEffectsAsync(VernukoDbContext db, BusinessSettings settings, Appointment appt, AppointmentStatus from, AppointmentStatus to, string? reason, CustomerPackage? package, CancellationToken ct)
    {
        var now = UtcNow;
        switch (to)
        {
            case AppointmentStatus.Completed when package is not null:
                return package.ConsumeFor(appt.Id, UserId, now);

            case AppointmentStatus.Completed when appt.PriceMinor > 0:
            {
                var open = await OpenChargesAsync(db, appt.Id, ct);
                if (open.Count == 0)
                {
                    var charge = CustomerTransaction.Create(appt.BranchId, appt.CustomerId, TransactionKind.Charge, new Money(appt.PriceMinor, settings.Currency), null, false,
                        appt.Id, null, "Hizmet bedeli", UserId, now, $"charge:appointment:{appt.Id:N}:{Ids.New():N}");
                    if (charge.IsFailure)
                    {
                        return charge.Error;
                    }

                    db.Transactions.Add(charge.Value);
                }

                return Result.Success();
            }

            case AppointmentStatus.InProgress when from == AppointmentStatus.Completed:
            {
                if (package is not null)
                {
                    return package.RestoreFor(appt.Id, UserId, now, reason ?? "Tamamlama geri alındı");
                }

                var charges = await OpenChargesAsync(db, appt.Id, ct);
                foreach (var c in charges)
                {
                    var rev = CustomerTransaction.Reverse(c, "Randevu tamamlaması geri alındı", UserId, now);
                    if (rev.IsFailure)
                    {
                        return rev.Error;
                    }

                    db.Transactions.Add(rev.Value);
                }

                return Result.Success();
            }

            case AppointmentStatus.NoShow when package is not null && settings.NoShowConsumesSession:
                return package.ConsumeFor(appt.Id, UserId, now);

            case AppointmentStatus.Arrived when from == AppointmentStatus.NoShow && package is not null:
                return package.RestoreFor(appt.Id, UserId, now, "Gelmedi kaydı düzeltildi");

            default:
                return Result.Success();
        }
    }

    private async Task<Result<(AppointmentSlot Slot, List<SchedulingIssue> Issues)>> PrepareSlotAsync(
        VernukoDbContext db, BusinessSettings settings, TimeZoneInfo zone, ServiceDefinition service, Guid staffId, IReadOnlyList<Guid> resourceIds,
        DateTime startLocal, int? durationOverride, Guid? ignoreAppointmentId, CancellationToken ct, IReadOnlySet<Guid>? alsoIgnore = null)
    {
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId, ct);
        if (staff is null || !staff.IsActive)
        {
            return Error.Validation("appointment.staff", "Personel bulunamadı veya pasif.");
        }

        var ids = resourceIds.Distinct().ToList();
        var resources = await db.Resources.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (resources.Count != ids.Count || resources.Any(x => !x.IsActive))
        {
            return Error.Validation("appointment.resource", "Seçilen oda/cihaz bulunamadı veya pasif.");
        }

        var duration = durationOverride ?? service.DurationMinutes;
        var local = new DateTime(startLocal.Year, startLocal.Month, startLocal.Day, startLocal.Hour, startLocal.Minute, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZones.ToUtc(local, zone);
        var slot = new AppointmentSlot(startUtc, duration, service.PrepMinutes, service.CleanupMinutes, staffId, ids, service.ProcessingStartMinutes, service.ProcessingMinutes);
        if (duration is < 5 or > 24 * 60)
        {
            return Error.Validation("appointment.duration", "Randevu süresi 5 dakika ile 24 saat arasında olmalıdır.");
        }

        var ctx = await AppointmentQueries.LoadDayContextAsync(db, settings, zone, DateOnly.FromDateTime(local), staffId, ids, ct);
        if (alsoIgnore is { Count: > 0 })
        {
            ctx = ctx with { Bookings = ctx.Bookings.Where(b => !alsoIgnore.Contains(b.AppointmentId)).ToList() };
        }

        var issues = AvailabilityCalculator.Validate(ctx, slot, ignoreAppointmentId).ToList();

        var linked = await db.StaffServices.AsNoTracking().Where(x => x.ServiceId == service.Id).Select(x => x.StaffMemberId).ToListAsync(ct);
        if (linked.Count > 0 && !linked.Contains(staffId))
        {
            issues.Add(new SchedulingIssue("staff.service", $"{staff.DisplayName} bu hizmeti verenler arasında tanımlı değil.", IssueSeverity.RequiresConfirmation));
        }

        var allowedIds = await db.ServiceResources.AsNoTracking().Where(x => x.ServiceId == service.Id).Select(x => x.ResourceId).ToListAsync(ct);
        if (allowedIds.Count > 0)
        {
            // Explicit rule: one allowed resource of every linked kind, nothing else of those kinds. A missing device
            // would also skip its double-booking check, so these are blocking, not just warnings.
            var allowed = await db.Resources.AsNoTracking().Where(x => allowedIds.Contains(x.Id) && x.IsActive).ToListAsync(ct);
            foreach (var group in allowed.GroupBy(x => x.Kind))
            {
                var kindName = BusinessProfileCatalog.ResourceKindName(group.Key).ToLower(TurkishText.Culture);
                var wrong = resources.Where(x => x.Kind == group.Key && group.All(a => a.Id != x.Id)).ToList();
                if (wrong.Count > 0)
                {
                    issues.Add(new SchedulingIssue("resource.not_allowed", $"{service.Name} {string.Join(", ", wrong.Select(w => w.Name))} ile yapılamaz. Uygun {kindName}: {string.Join(", ", group.Select(a => a.Name))}.", IssueSeverity.Blocking));
                }
                else if (resources.All(x => x.Kind != group.Key))
                {
                    issues.Add(new SchedulingIssue("resource.missing", $"{service.Name} için {kindName} seçilmeli: {string.Join(" veya ", group.Select(a => a.Name))}.", IssueSeverity.Blocking));
                }
            }
        }
        else if (service.RequiredResourceKind is { } kind && resources.All(x => x.Kind != kind))
        {
            issues.Add(new SchedulingIssue("resource.missing", $"Bu hizmet için {BusinessProfileCatalog.ResourceKindName(kind).ToLower(TurkishText.Culture)} seçilmedi.", IssueSeverity.RequiresConfirmation));
        }

        if (startUtc < UtcNow.AddMinutes(-5))
        {
            issues.Add(new SchedulingIssue("time.past", "Geçmiş bir saate randevu kaydediyorsunuz.", IssueSeverity.RequiresConfirmation));
        }

        return (slot, issues);
    }

    /// <summary>Service charges of an appointment that have not been reversed.</summary>
    private static async Task<List<CustomerTransaction>> OpenChargesAsync(VernukoDbContext db, Guid appointmentId, CancellationToken ct)
    {
        var charges = await db.Transactions.Where(t => t.AppointmentId == appointmentId && t.Kind == TransactionKind.Charge).ToListAsync(ct);
        var ids = charges.Select(c => (Guid?)c.Id).ToList();
        var reversed = await db.Transactions.Where(t => ids.Contains(t.ReversesTransactionId)).Select(t => t.ReversesTransactionId).ToListAsync(ct);
        return charges.Where(c => !reversed.Contains(c.Id)).ToList();
    }

    private static Task<int> CountOpenPackageAppointmentsAsync(VernukoDbContext db, Guid packageId, Guid? exclude, CancellationToken ct)
    {
        var open = new[] { AppointmentStatus.Created, AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Waiting, AppointmentStatus.InProgress };
        return db.Appointments.CountAsync(a => a.CustomerPackageId == packageId && a.Id != exclude && open.Contains(a.Status), ct);
    }

    private static IQueryable<Appointment> Filtered(VernukoDbContext db, AppointmentFilter f)
    {
        var q = db.Appointments.AsNoTracking();
        if (f.StaffMemberId is { } staff)
        {
            q = q.Where(a => a.StaffMemberId == staff);
        }

        if (f.ServiceId is { } service)
        {
            q = q.Where(a => a.ServiceId == service);
        }

        if (f.ResourceId is { } resource)
        {
            q = q.Where(a => a.Bookings.Any(b => b.ResourceId == resource));
        }

        if (!f.IncludeCancelled)
        {
            q = q.Where(a => a.Status != AppointmentStatus.Cancelled);
        }

        return q;
    }
}
