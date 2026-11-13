using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Packages;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed class CustomerService(AppServiceDeps deps, Vernuko.Application.Common.IAppEnvironment env) : AppServiceBase(deps), ICustomerService
{
    public const int DefaultSearchLimit = 50;

    private static readonly JsonSerializerOptions ExportJson = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Turkish-aware search on name, phone (any format) and customer number. Archived customers optional.</summary>
    public Task<Result<IReadOnlyList<CustomerListItem>>> SearchAsync(string? query, bool includeArchived = false, int take = DefaultSearchLimit, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CustomerListItem>>(Permission.CustomersView, async db =>
        {
            var q = db.Customers.AsNoTracking().Where(c => c.State != CustomerState.Anonymized);
            if (!includeArchived)
            {
                q = q.Where(c => c.State == CustomerState.Active);
            }

            foreach (var term in SearchTerms(query))
            {
                var pattern = "%" + term + "%";
                q = q.Where(c => EF.Functions.Like(c.SearchKey, pattern));
            }

            var rows = await q.OrderBy(c => c.FirstName).ThenBy(c => c.LastName).Take(Math.Clamp(take, 1, 500))
                .Select(c => new { c.Id, c.Number, c.FirstName, c.LastName, c.Phone, c.Email, c.State })
                .ToListAsync(ct);
            var ids = rows.Select(r => r.Id).ToList();
            var lastVisits = await db.Appointments.AsNoTracking()
                .Where(a => ids.Contains(a.CustomerId) && a.Status == AppointmentStatus.Completed)
                .GroupBy(a => a.CustomerId)
                .Select(g => new { g.Key, Last = g.Max(a => a.StartUtc) })
                .ToDictionaryAsync(x => x.Key, x => (DateTime?)x.Last, ct);
            return rows.Select(r => new CustomerListItem(
                r.Id,
                r.Number,
                string.IsNullOrEmpty(r.LastName) ? r.FirstName : $"{r.FirstName} {r.LastName}",
                FormatPhone(r.Phone),
                r.Email,
                r.State.ToString(),
                lastVisits.GetValueOrDefault(r.Id))).ToList();
        }, ct);

    public Task<Result<CustomerDetailsDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        ReadAsync<CustomerDetailsDto>(Permission.CustomersView, async db =>
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            var balance = await db.Transactions.Where(t => t.CustomerId == id).SumAsync(t => t.BalanceEffectMinor, ct);
            return ToDto(c, balance, Session.Has(Permission.CustomerSensitiveDataView));
        }, ct);

    public Task<Result<IReadOnlyList<DuplicateCandidate>>> FindDuplicatesAsync(string firstName, string lastName, string? phone, Guid? excludeId = null, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<DuplicateCandidate>>(Permission.CustomersView, async db =>
        {
            var fullName = TurkishText.ToNameCase($"{firstName} {lastName}");
            var phoneE164 = string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Parse(phone) is { IsSuccess: true } p ? p.Value.E164 : null;
            var key = TurkishText.ToSearchKey(fullName);
            var candidates = await db.Customers.AsNoTracking()
                .Where(c => c.State != CustomerState.Anonymized && ((phoneE164 != null && c.Phone == phoneE164) || EF.Functions.Like(c.SearchKey, key + "%")))
                .Take(50)
                .ToListAsync(ct);
            return DuplicateDetector.Find(fullName, phoneE164, candidates, excludeId).ToList();
        }, ct);

    /// <summary>
    /// Creates a customer. Possible duplicates are returned as a failure with code "customer.possible_duplicate"
    /// unless <paramref name="allowDuplicates"/> is true (the user confirmed they are different people).
    /// </summary>
    public Task<Result<Guid>> CreateAsync(CustomerDetails details, bool allowDuplicates, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.CustomersManage, async db =>
        {
            var number = (await db.Customers.MaxAsync(c => (int?)c.Number, ct) ?? 0) + 1;
            var created = Customer.Create(number, details);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var customer = created.Value;
            if (!allowDuplicates)
            {
                var key = TurkishText.ToSearchKey(customer.FullName);
                var existing = await db.Customers.AsNoTracking()
                    .Where(c => c.State != CustomerState.Anonymized && ((customer.Phone != null && c.Phone == customer.Phone) || EF.Functions.Like(c.SearchKey, key + "%")))
                    .Take(50)
                    .ToListAsync(ct);
                var dups = DuplicateDetector.Find(customer.FullName, customer.Phone, existing);
                if (dups.Count > 0)
                {
                    return CustomerErrors.PossibleDuplicate(dups);
                }
            }

            db.Customers.Add(customer);
            await Audit.AppendAsync(db, "customer.created", "Customer", customer.Id, $"Müşteri #{customer.Number} oluşturuldu", ct);
            return customer.Id;
        }, ct);

    public Task<Result<bool>> UpdateAsync(Guid id, long expectedVersion, CustomerDetails details, bool allowSms, bool allowWhatsApp, bool allowEmail, bool marketingConsent, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.CustomersManage, async db =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            db.Entry(c).Property(x => x.Version).OriginalValue = expectedVersion;

            // Users without the sensitive-data permission cannot see the national id, so they must not wipe it either.
            if (!Session.Has(Permission.CustomerSensitiveDataView))
            {
                details = details with { NationalId = c.NationalId };
            }

            var r = c.Update(details);
            if (r.IsFailure)
            {
                return r.Error;
            }

            c.SetContactPreferences(allowSms, allowWhatsApp, allowEmail);
            if (c.MarketingConsent != marketingConsent)
            {
                c.SetMarketingConsent(marketingConsent, UtcNow, "Ön büro");
            }

            await Audit.AppendAsync(db, "customer.updated", "Customer", c.Id, $"Müşteri #{c.Number} güncellendi", ct);
            return true;
        }, ct);

    public Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.CustomersArchiveOrAnonymize, async db =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            var r = c.Archive(UtcNow);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "customer.archived", "Customer", c.Id, $"Müşteri #{c.Number} arşivlendi", ct);
            return true;
        }, ct);

    public Task<Result<bool>> RestoreAsync(Guid id, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.CustomersArchiveOrAnonymize, async db =>
        {
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            var r = c.Restore();
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "customer.restored", "Customer", c.Id, $"Müşteri #{c.Number} arşivden çıkarıldı", ct);
            return true;
        }, ct);

    /// <summary>
    /// Irreversibly removes personal data (KVKK erasure request). Future open appointments are cancelled first.
    /// Financial and appointment statistics are kept without identity.
    /// </summary>
    public Task<Result<bool>> AnonymizeAsync(Guid id, string reason, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.CustomersArchiveOrAnonymize, async db =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Error.Validation("customer.anonymize_reason", "Anonimleştirme nedeni zorunludur.");
            }

            var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            var now = UtcNow;
            var future = await db.Appointments.Include(a => a.Bookings).Include(a => a.History)
                .Where(a => a.CustomerId == id && a.StartUtc > now)
                .ToListAsync(ct);
            foreach (var a in future.Where(a => AppointmentStatusRules.CanTransition(a.Status, AppointmentStatus.Cancelled)))
            {
                a.ChangeStatus(AppointmentStatus.Cancelled, now, UserId, "Müşteri verisi anonimleştirildi");
                a.UpdateNotes(null);
            }

            await CustomerRecordsService.DeleteAllForCustomerAsync(db, env, c.Id, now, ct);
            c.Anonymize(now);
            foreach (var note in await db.PrivateNotes.Where(n => n.CustomerId == c.Id && n.DeletedAtUtc == null).ToListAsync(ct))
            {
                note.Delete(now); // specialists' notes are personal (often health) data too
            }
            await Audit.AppendAsync(db, "customer.anonymized", "Customer", c.Id, $"Müşteri #{c.Number} anonimleştirildi. Gerekçe kaydedildi.", ct);
            return true;
        }, ct);

    /// <summary>Data-subject export (KVKK md. 11): everything Vernuko stores about one customer, as JSON.</summary>
    public Task<Result<string>> ExportCustomerJsonAsync(Guid id, CancellationToken ct = default) =>
        WriteAlwaysAsync<string>(Permission.CustomersExport, async db =>
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Error.NotFound("Müşteri");
            }

            var appointments = await db.Appointments.AsNoTracking().Where(a => a.CustomerId == id).OrderBy(a => a.StartUtc)
                .Join(db.Services, a => a.ServiceId, s => s.Id, (a, s) => new { a.StartUtc, a.EndUtc, Service = s.Name, Status = a.Status.ToString(), a.Notes, a.BodyArea })
                .ToListAsync(ct);
            var packages = await db.CustomerPackages.AsNoTracking().Include(p => p.Entries).Where(p => p.CustomerId == id).ToListAsync(ct);
            var transactions = await db.Transactions.AsNoTracking().Where(t => t.CustomerId == id).OrderBy(t => t.OccurredAtUtc).ToListAsync(ct);
            var export = new
            {
                exportedAtUtc = UtcNow,
                customer = new
                {
                    c.Number, c.FirstName, c.LastName, c.Phone, c.Email, c.BirthDate, c.Address, c.Notes, c.NationalId, c.CompanyName, c.TaxOffice, c.TaxNumber,
                    c.AllowSms, c.AllowWhatsApp, c.AllowEmail, c.MarketingConsent, c.MarketingConsentAtUtc, State = c.State.ToString(), c.CreatedAtUtc,
                },
                appointments,
                packages = packages.Select(p => new { p.Name, p.BodyArea, p.TotalSessions, p.UsedSessions, p.RemainingSessions, p.PurchasedAtUtc, p.ExpiresAtUtc, Status = p.Status.ToString() }),
                transactions = transactions.Select(t => new { Kind = t.Kind.ToString(), Amount = t.AmountMinor / 100m, Method = t.Method?.ToString(), t.IsDeposit, t.Description, t.OccurredAtUtc }),
            };
            await Audit.AppendAsync(db, "customer.exported", "Customer", c.Id, $"Müşteri #{c.Number} verileri dışa aktarıldı", ct);
            return JsonSerializer.Serialize(export, ExportJson);
        }, ct);

    /// <summary>All active customers as CSV (UTF-8 with BOM so Excel shows Turkish characters correctly).</summary>
    public Task<Result<string>> ExportAllCsvAsync(CancellationToken ct = default) =>
        WriteAlwaysAsync<string>(Permission.CustomersExport, async db =>
        {
            var rows = await db.Customers.AsNoTracking().Where(c => c.State != CustomerState.Anonymized).OrderBy(c => c.Number).ToListAsync(ct);
            var sb = new StringBuilder();
            sb.Append('﻿');
            sb.AppendLine("No;Ad;Soyad;Telefon;E-posta;Doğum Tarihi;Firma;Vergi Dairesi;Vergi No;Durum;Kayıt Tarihi");
            foreach (var c in rows)
            {
                sb.AppendLine(string.Join(';', c.Number.ToString(CultureInfo.InvariantCulture), Csv(c.FirstName), Csv(c.LastName), Csv(c.DisplayPhone), Csv(c.Email),
                    c.BirthDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? string.Empty, Csv(c.CompanyName), Csv(c.TaxOffice), Csv(c.TaxNumber), c.State.ToString(), c.CreatedAtUtc.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)));
            }

            await Audit.AppendAsync(db, "customers.exported_csv", "Customer", null, $"{rows.Count} müşteri CSV olarak dışa aktarıldı", ct);
            return sb.ToString();
        }, ct);

    public Task<Result<IReadOnlyList<AppointmentListItem>>> GetHistoryAsync(Guid customerId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<AppointmentListItem>>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var ids = await db.Appointments.AsNoTracking().Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.StartUtc).Take(200).Select(a => a.Id).ToListAsync(ct);
            return Result.Success(await AppointmentQueries.LoadItemsAsync(db, ids, zone, ct));
        }, ct);

    internal static CustomerDetailsDto ToDto(Customer c, long balance, bool canSeeSensitive) => new(
        c.Id, c.Number, c.FirstName, c.LastName, c.DisplayPhone, c.Email, c.BirthDate, c.Address, c.Notes,
        c.NationalId is null ? null : canSeeSensitive ? c.NationalId : "*******" + c.NationalId[^4..],
        c.AllowSms, c.AllowWhatsApp, c.AllowEmail, c.MarketingConsent, c.State.ToString(), balance, c.Version, c.CompanyName, c.TaxOffice, c.TaxNumber);

    internal static string? FormatPhone(string? e164) =>
        e164 is null ? null : PhoneNumber.Parse(e164) is { IsSuccess: true } p ? p.Value.ToDisplayString() : e164;

    private static IEnumerable<string> SearchTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        foreach (var raw in query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5))
        {
            // Phone fragments: strip formatting so "0532 456" and "0532456" both match.
            var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
            var term = digits.Length >= 3 && digits.Length == raw.Count(ch => !(ch is '(' or ')' or '-' or '+' or '.')) ? digits : TurkishText.ToSearchKey(raw);
            if (term.Length > 0)
            {
                yield return term.Replace("%", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
            }
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Neutralise spreadsheet formula injection.
        var safe = value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
        return safe.Contains(';', StringComparison.Ordinal) || safe.Contains('"', StringComparison.Ordinal) || safe.Contains('\n', StringComparison.Ordinal)
            ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : safe;
    }
}

public static class CustomerErrors
{
    public const string PossibleDuplicateCode = "customer.possible_duplicate";

    public static Error PossibleDuplicate(IReadOnlyList<DuplicateCandidate> candidates) =>
        new(PossibleDuplicateCode, "Benzer müşteri kaydı bulundu: " + string.Join("; ", candidates.Take(3).Select(c => $"#{c.Number} {c.FullName} — {c.Explanation}")));
}
