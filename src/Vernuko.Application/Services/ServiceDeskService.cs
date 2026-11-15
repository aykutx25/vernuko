using System.Text;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Commerce;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;
using Vernuko.ServiceDesk;

namespace Vernuko.Application.Services;

/// <summary>
/// Teknik Servis mode: a device from intake to delivery. Parts leave stock when they are fitted (a removed line puts
/// them back); money is booked at delivery on the customer's account; every step is in the ticket's history.
/// </summary>
public sealed class ServiceDeskService(AppServiceDeps deps, ISecretProtector protector, AutoMessagingService messaging) : AppServiceBase(deps), IServiceDeskService
{
    public const string NumberPrefix = "SF";

    public Task<Result<TicketDto>> OpenTicketAsync(OpenTicketRequest r, CancellationToken ct = default) =>
        WriteAsync<TicketDto>(Permission.ServiceTicketsManage, async db =>
        {
            var key = $"ticket:{r.IdempotencyKey}";
            if (await db.ServiceTickets.AsNoTracking().Where(t => EF.Property<string?>(t, PersistenceNames.DocumentKey) == key).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct) is { } done)
            {
                return await LoadAsync(db, done, ct);
            }

            if (await CheckIntakeAsync(db, r.Intake, ct) is { } bad)
            {
                return bad;
            }

            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var today = TimeZones.LocalDate(UtcNow, zone);
            var head = $"{NumberPrefix}-{today.Year}-";
            var last = await db.ServiceTickets.Select(t => t.Number).Where(n => n.StartsWith(head)).OrderByDescending(n => n).FirstOrDefaultAsync(ct);
            var number = DocumentNumbers.Format(NumberPrefix, today.Year, (last is null ? 0 : DocumentNumbers.SequenceOf(last, NumberPrefix, today.Year)) + 1);
            var opened = ServiceTicket.Open(number, ToIntake(r.Intake), UserId, UtcNow, today, Protect(r.UnlockCode));
            if (opened.IsFailure)
            {
                return opened.Error;
            }

            var t = opened.Value;
            db.ServiceTickets.Add(t);
            db.Entry(t).Property(PersistenceNames.DocumentKey).CurrentValue = key;
            Log(db, t, TicketEventKind.Created, $"Cihaz teslim alındı: {DeviceText(t)}. Söz verilen tarih {t.DueDate:dd.MM.yyyy}.");
            await Audit.AppendAsync(db, "ticket.opened", "ServiceTicket", t.Id, $"{t.Number}: {DeviceText(t)}", ct);
            await db.SaveChangesAsync(ct);
            return await LoadAsync(db, t.Id, ct);
        }, ct);

    public Task<Result<TicketDto>> UpdateIntakeAsync(Guid ticketId, TicketIntakeRequest intake, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            if (await CheckIntakeAsync(db, intake, ct) is { } bad)
            {
                return bad;
            }

            // A kapora was paid on the first customer's account: moving the ticket would leave it there.
            if (intake.CustomerId != t.CustomerId && await DepositsAsync(db, t, ct) > 0)
            {
                return Error.Validation("ticket.customer_deposit", "Bu fişe kapora alınmış; müşteri değiştirilemez. Yeni müşteri için ayrı fiş açın.");
            }

            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var r = t.UpdateIntake(ToIntake(intake), TimeZones.LocalDate(t.ReceivedAtUtc, zone));
            return r.IsFailure ? r.Error : "Kabul bilgileri güncellendi.";
        }, ct);

    public Task<Result<TicketDto>> SetDiagnosisAsync(Guid ticketId, string? diagnosis, string? workDone, CancellationToken ct = default) =>
        Change(ticketId, (_, t) =>
        {
            var r = t.SetDiagnosis(diagnosis, workDone);
            return Task.FromResult<Result<string>>(r.IsFailure ? r.Error : "Tespit ve yapılan işlem güncellendi.");
        }, ct);

    public Task<Result<TicketDto>> SetEstimateAsync(Guid ticketId, long amountMinor, long diagnosticFeeMinor, CancellationToken ct = default) =>
        Change(ticketId, (_, t) =>
        {
            var r = t.SetEstimate(amountMinor, diagnosticFeeMinor, UtcNow);
            return Task.FromResult<Result<string>>(r.IsFailure ? r.Error
                : $"Fiyat teklifi: {new Money(amountMinor)}" + (diagnosticFeeMinor > 0 ? $", onaylanmazsa tespit ücreti {new Money(diagnosticFeeMinor)}" : string.Empty) + ". Müşteri onayı bekleniyor.");
        }, ct, TicketEventKind.EstimateSet);

    public Task<Result<TicketDto>> RecordApprovalAsync(Guid ticketId, bool approved, ApprovalChannel channel, string? approvedBy, CancellationToken ct = default) =>
        Change(ticketId, (_, t) =>
        {
            var r = t.RecordApproval(approved, channel, approvedBy, UtcNow);
            return Task.FromResult<Result<string>>(r.IsFailure ? r.Error
                : $"{ServiceRules.ApprovalChannelName(channel)} {(approved ? "onaylandı" : "onaylanmadı")}.");
        }, ct, TicketEventKind.ApprovalRecorded);

    public Task<Result<TicketDto>> WithdrawEstimateAsync(Guid ticketId, CancellationToken ct = default) =>
        Change(ticketId, (_, t) =>
        {
            var r = t.WithdrawEstimate(UtcNow);
            return Task.FromResult<Result<string>>(r.IsFailure ? r.Error : "Fiyat teklifi geri çekildi.");
        }, ct, TicketEventKind.EstimateSet);

    /// <summary>Ready or unrepairable with notifyCustomer: an SMS through the shop's own account (when set up and allowed).</summary>
    public Task<Result<TicketDto>> ChangeStatusAsync(Guid ticketId, TicketStatus status, string? reason, bool notifyCustomer, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            var from = t.Status;
            var r = t.ChangeStatus(status, reason, UtcNow);
            if (r.IsFailure)
            {
                return r.Error;
            }

            var text = $"{ServiceRules.StatusName(from)} → {ServiceRules.StatusName(status)}" + (status == TicketStatus.Unrepairable ? $": {t.UnrepairableReason}" : string.Empty);
            if (notifyCustomer && status is TicketStatus.Ready or TicketStatus.Unrepairable)
            {
                var why = await messaging.QueueServiceReadyAsync(db, t.CustomerId, t.Number, $"service-ready:{t.Id}:{status}", ct);
                Log(db, t, TicketEventKind.MessageQueued, why is null ? "Müşteriye \"teslime hazır\" SMS'i sıraya alındı." : $"SMS gönderilmedi: {why}");
            }

            return text;
        }, ct, TicketEventKind.StatusChanged);

    public Task<Result<TicketDto>> LinkWarrantyAsync(Guid ticketId, Guid? earlierTicketId, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            if (earlierTicketId is { } id)
            {
                var earlier = await db.ServiceTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == TicketStatus.Delivered && x.Repaired, ct);
                if (earlier is null || earlier.Id == t.Id)
                {
                    return Error.Validation("ticket.warranty", "Garanti kapsamı için teslim edilmiş, onarılmış bir servis fişi seçin.");
                }

                // Only the same device, and only while its same-fault warranty runs (SSHY m.10/6: 1 year).
                var (_, zone) = await BusinessContext.LoadAsync(db, ct);
                if (t.SerialNumber is null || earlier.SerialNumber != t.SerialNumber)
                {
                    return Error.Validation("ticket.warranty_device", "Garanti yalnızca aynı seri numaralı cihazın önceki onarımı için geçerlidir.");
                }

                if (earlier.WarrantyUntil(zone) is not { } w || w.SameFault < TimeZones.LocalDate(UtcNow, zone))
                {
                    return Error.Validation("ticket.warranty_expired", $"{earlier.Number} fişinin aynı arıza garantisi sona ermiş.");
                }

                t.LinkWarranty(id);
                return $"Onarım garantisi kapsamında ({earlier.Number}): işçilik ücretlendirilmez.";
            }

            t.LinkWarranty(null);
            return "Garanti bağlantısı kaldırıldı.";
        }, ct);

    /// <summary>A part from stock: it leaves stock now (fitted into the device).</summary>
    public Task<Result<TicketDto>> AddPartAsync(Guid ticketId, Guid productId, int quantity, long? unitPriceMinor, string? idempotencyKey = null, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            if (await AlreadyAddedAsync(db, idempotencyKey, ct))
            {
                return Result.Success("Parça zaten eklenmiş.");
            }

            var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId && x.IsActive, ct);
            if (p is null)
            {
                return Error.NotFound("Parça");
            }

            var movement = StockMovement.Create(p.Id, StockMovementKind.Consumed, quantity, p.CostMinor, null, t.CustomerId, $"Servis {t.Number}", UserId, UtcNow, t.Id);
            if (movement.IsFailure)
            {
                return movement.Error;
            }

            var line = t.AddLine(TicketLineKind.Part, p.Id, p.Name, quantity, unitPriceMinor ?? p.SalePriceMinor, p.VatRate, null, movement.Value.Id);
            if (line.IsFailure)
            {
                return line.Error;
            }

            db.StockMovements.Add(movement.Value);
            db.ServiceTicketLines.Add(line.Value);
            Remember(db, line.Value, idempotencyKey);
            return $"Parça: {p.Name} × {quantity} ({new Money(line.Value.TotalMinor)})";
        }, ct, TicketEventKind.LineAdded);

    public Task<Result<TicketDto>> AddLaborAsync(Guid ticketId, string description, long priceMinor, Guid? staffId, string? idempotencyKey = null, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            if (await AlreadyAddedAsync(db, idempotencyKey, ct))
            {
                return Result.Success("İşçilik zaten eklenmiş.");
            }

            if (staffId is { } sid && !await db.Staff.AsNoTracking().AnyAsync(s => s.Id == sid, ct))
            {
                return Error.NotFound("Teknisyen");
            }

            var line = t.AddLine(TicketLineKind.Labor, null, description, 1, priceMinor, 20, staffId, null);
            if (line.IsFailure)
            {
                return line.Error;
            }

            db.ServiceTicketLines.Add(line.Value);
            Remember(db, line.Value, idempotencyKey);
            return $"İşçilik: {line.Value.Description} ({new Money(priceMinor)})";
        }, ct, TicketEventKind.LineAdded);

    public Task<Result<TicketDto>> RemoveLineAsync(Guid ticketId, Guid lineId, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            var removed = t.RemoveLine(lineId);
            if (removed.IsFailure)
            {
                return removed.Error;
            }

            if (removed.Value.StockMovementId is { } mid && await db.StockMovements.FirstOrDefaultAsync(m => m.Id == mid, ct) is { } original)
            {
                db.StockMovements.Add(StockMovement.Reverse(original, $"Servis {t.Number}: parça geri alındı", UserId, UtcNow));
            }

            return $"Satır kaldırıldı: {removed.Value.Description}";
        }, ct, TicketEventKind.LineRemoved);

    /// <summary>Kapora: a payment on the customer's account before the work; it is offset at delivery.</summary>
    public Task<Result<TicketDto>> TakeDepositAsync(Guid ticketId, long amountMinor, PaymentMethod method, string idempotencyKey, CancellationToken ct = default) =>
        Change(ticketId, async (db, t) =>
        {
            var key = $"ticket-deposit:{idempotencyKey}";
            if (await db.Transactions.AsNoTracking().AnyAsync(x => x.IdempotencyKey == key, ct))
            {
                return "Kapora zaten kayıtlı.";
            }

            if (!t.IsOpen)
            {
                return Error.Validation("ticket.closed", "Teslim edilmiş fişe kapora alınamaz.");
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var tx = CustomerTransaction.Create(Branch.DefaultId, t.CustomerId, TransactionKind.Payment, new Money(amountMinor, settings.Currency), method, true, null, null, $"Servis {t.Number} kaporası", UserId, UtcNow, key);
            if (tx.IsFailure)
            {
                return tx.Error;
            }

            db.Transactions.Add(tx.Value);
            return $"Kapora alındı: {new Money(amountMinor)} ({CustomerTransaction.MethodName(method)})";
        }, ct, TicketEventKind.Note);

    public Task<Result<TicketDto>> DeliverAsync(DeliverTicketRequest r, CancellationToken ct = default) =>
        WriteAsync<TicketDto>(Permission.ServiceTicketsManage, async db =>
        {
            var key = $"ticket-deliver:{r.IdempotencyKey}";
            var t = await db.ServiceTickets.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == r.TicketId, ct);
            if (t is null)
            {
                return Error.NotFound("Servis fişi");
            }

            if (t.Status == TicketStatus.Delivered)
            {
                // The same delivery again (double click, retry after a timeout), also when nothing was charged or paid:
                // a delivered ticket cannot be delivered twice, so the current state is the answer.
                return await LoadAsync(db, t.Id, ct);
            }

            if (r.CashMinor < 0 || r.CardMinor < 0 || r.TransferMinor < 0)
            {
                return Error.Validation("ticket.payment", "Ödeme tutarı negatif olamaz.");
            }

            var delivered = t.Deliver(r.DeliveredTo, UtcNow);
            if (delivered.IsFailure)
            {
                return delivered.Error;
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var money = (long a) => new Money(a, settings.Currency);
            if (t.TotalChargedMinor > 0)
            {
                db.Transactions.Add(CustomerTransaction.Create(Branch.DefaultId, t.CustomerId, TransactionKind.Charge, money(t.TotalChargedMinor), null, false, null, null, $"Servis {t.Number}" + (t.Repaired ? string.Empty : " (onarılmadı, tespit ücreti)"), UserId, UtcNow, $"{key}:charge").Value);
            }

            foreach (var (amount, method) in new[] { (r.CashMinor, PaymentMethod.Cash), (r.CardMinor, PaymentMethod.Card), (r.TransferMinor, PaymentMethod.BankTransfer) })
            {
                if (amount > 0)
                {
                    db.Transactions.Add(CustomerTransaction.Create(Branch.DefaultId, t.CustomerId, TransactionKind.Payment, money(amount), method, false, null, null, $"Servis {t.Number} ödemesi", UserId, UtcNow, $"{key}:pay:{method}").Value);
                }
            }

            Log(db, t, TicketEventKind.Delivered, "Teslim edildi. " + (t.Repaired ? "Onarıldı." : "Onarılmadan iade.") + $" Tutar {money(t.TotalChargedMinor)}." + (t.UnlockCodeProtected is null ? " Cihaz şifresi silindi." : string.Empty));
            await Audit.AppendAsync(db, "ticket.delivered", "ServiceTicket", t.Id, $"{t.Number}: {money(t.TotalChargedMinor)}", ct);
            await db.SaveChangesAsync(ct);
            return await LoadAsync(db, t.Id, ct);
        }, ct);

    /// <summary>Shows the stored unlock code to a user who may manage tickets; the viewing is logged.</summary>
    public Task<Result<string>> RevealUnlockCodeAsync(Guid ticketId, CancellationToken ct = default) =>
        WriteAsync<string>(Permission.ServiceTicketsManage, async db =>
        {
            var t = await db.ServiceTickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t is null)
            {
                return Error.NotFound("Servis fişi");
            }

            if (t.UnlockCodeProtected is null)
            {
                return Error.Validation("ticket.no_code", "Bu cihaz için kayıtlı şifre yok.");
            }

            Log(db, t, TicketEventKind.UnlockCodeViewed, "Cihaz şifresi görüntülendi.");
            await Audit.AppendAsync(db, "ticket.unlock_viewed", "ServiceTicket", t.Id, t.Number, ct);
            return Encoding.UTF8.GetString(protector.Unprotect(t.UnlockCodeProtected));
        }, ct);

    public Task<Result<TicketDto>> SetUnlockCodeAsync(Guid ticketId, string? code, CancellationToken ct = default) =>
        Change(ticketId, (_, t) =>
        {
            t.SetUnlockCode(Protect(code));
            return Task.FromResult<Result<string>>(string.IsNullOrWhiteSpace(code) ? "Cihaz şifresi silindi." : "Cihaz şifresi kaydedildi (şifreli).");
        }, ct);

    public Task<Result<TicketDto>> GetTicketAsync(Guid ticketId, CancellationToken ct = default) =>
        ReadAsync<TicketDto>(Permission.ServiceTicketsView, db => LoadAsync(db, ticketId, ct), ct);

    /// <summary>Open tickets (or all), newest first; search by number, serial/IMEI, brand/model or customer.</summary>
    public Task<Result<IReadOnlyList<TicketSummaryDto>>> GetTicketsAsync(bool openOnly = true, string? search = null, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<TicketSummaryDto>>(Permission.ServiceTicketsView, async db =>
        {
            var q = db.ServiceTickets.AsNoTracking();
            if (openOnly)
            {
                q = q.Where(t => t.Status != TicketStatus.Delivered);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var serial = ServiceRules.NormalizeSerial(s) ?? s;
                // Customers as the Customers screen finds them: Turkish-insensitive name, company, phone in any format.
                var matching = db.Customers.AsQueryable();
                foreach (var term in CustomerService.SearchTerms(s))
                {
                    var pattern = "%" + term + "%";
                    matching = matching.Where(c => EF.Functions.Like(c.SearchKey, pattern));
                }

                var customerIds = matching.Select(c => c.Id);
                q = q.Where(t => t.Number.Contains(s) || (t.SerialNumber != null && t.SerialNumber.Contains(serial)) || t.Brand.Contains(s) || (t.Model != null && t.Model.Contains(s)) || customerIds.Contains(t.CustomerId));
            }

            var tickets = await q.OrderByDescending(t => t.ReceivedAtUtc).Take(500).ToListAsync(ct);
            return Result.Success<IReadOnlyList<TicketSummaryDto>>(await SummariesAsync(db, tickets, ct));
        }, ct);

    public Task<Result<IReadOnlyList<TicketSummaryDto>>> GetDeviceHistoryAsync(string serialNumber, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<TicketSummaryDto>>(Permission.ServiceTicketsView, async db =>
        {
            var serial = ServiceRules.NormalizeSerial(serialNumber);
            if (serial is null)
            {
                return Result.Success<IReadOnlyList<TicketSummaryDto>>([]);
            }

            var tickets = await db.ServiceTickets.AsNoTracking().Where(t => t.SerialNumber == serial).OrderByDescending(t => t.ReceivedAtUtc).ToListAsync(ct);
            return Result.Success<IReadOnlyList<TicketSummaryDto>>(await SummariesAsync(db, tickets, ct));
        }, ct);

    /// <summary>The intake document or the delivery slip, with the fields SSHY m.11 asks for. Never the unlock code.</summary>
    public Task<Result<ServiceSlipDto>> GetSlipAsync(Guid ticketId, SlipKind kind, CancellationToken ct = default) =>
        ReadAsync<ServiceSlipDto>(Permission.ServiceTicketsView, async db =>
        {
            var full = await LoadAsync(db, ticketId, ct);
            if (full.IsFailure)
            {
                return full.Error;
            }

            var t = full.Value;
            if (kind == SlipKind.Delivery && t.DeliveredAtUtc is null)
            {
                return Error.Validation("ticket.not_delivered", "Servis fişi teslimde düzenlenir; cihaz henüz teslim edilmedi.");
            }

            var (business, zone) = await BusinessContext.LoadAsync(db, ct);
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == t.CustomerId, ct);
            var receivedBy = await db.ServiceTickets.AsNoTracking().Where(x => x.Id == ticketId).Select(x => x.ReceivedByUserId).FirstAsync(ct) is { } uid
                ? await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) : null;
            var notes = new List<string>
            {
                "Bu belge bir özel servis (yetkili servis değil) tarafından düzenlenmiştir.",
                "Belgenin bir örneği müşteriye verilir. Bu belge fatura veya mali fiş yerine geçmez.",
            };
            if (kind == SlipKind.Intake)
            {
                notes.Add($"Tahmini teslim tarihi: {t.Summary.DueDate:dd.MM.yyyy}. Onarımdan önce cihazınızdaki verilerin yedeğini almanız önerilir.");
            }
            else if (t.Repaired)
            {
                notes.Add($"Onarımdan sonra aynı arıza {ServiceRules.SameFaultWarrantyMonths} ay içinde tekrarlarsa ücretsiz giderilir (SSHY m.10/6). Değiştirilen parçalar en az {ServiceRules.PartsWarrantyMonths} ay garantilidir (SSHY m.10/7).");
            }

            var taxText = business.TaxOffice is null && business.TaxNumber is null ? null : $"{business.TaxOffice} V.D. {business.TaxNumber}".Trim();
            return new ServiceSlipDto(kind, business.BusinessName, business.Address, business.Phone, taxText, t.Summary.Number,
                customer.CompanyName ?? $"{customer.FirstName} {customer.LastName}", CustomerService.FormatPhone(customer.Phone), customer.Address,
                t.Summary.DeviceText, t.Summary.SerialNumber, t.Intake.Complaint, t.Intake.Accessories, t.Intake.Condition, t.Intake.BroughtBy,
                TimeZones.ToLocal(t.Summary.ReceivedAtUtc, zone), receivedBy, t.Summary.DueDate,
                kind == SlipKind.Delivery ? t.Diagnosis : null, kind == SlipKind.Delivery ? t.WorkDone : null,
                kind == SlipKind.Delivery ? t.Lines : [], kind == SlipKind.Delivery ? t.TotalChargedMinor : 0, t.Repaired, t.UnrepairableReason,
                t.DeliveredAtUtc is { } d ? TimeZones.ToLocal(d, zone) : null, t.DeliveredTo, t.SameFaultWarrantyUntil, t.PartsWarrantyUntil, notes);
        }, ct);

    // ------------------------------------------------------------------ helpers

    private Task<Result<TicketDto>> Change(Guid ticketId, Func<VernukoDbContext, ServiceTicket, Task<Result<string>>> work, CancellationToken ct, TicketEventKind kind = TicketEventKind.Note) =>
        WriteAsync<TicketDto>(Permission.ServiceTicketsManage, async db =>
        {
            var t = await db.ServiceTickets.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t is null)
            {
                return Error.NotFound("Servis fişi");
            }

            var r = await work(db, t);
            if (r.IsFailure)
            {
                return r.Error;
            }

            Log(db, t, kind, r.Value);
            await db.SaveChangesAsync(ct);
            return await LoadAsync(db, t.Id, ct);
        }, ct);

    private static async Task<Error?> CheckIntakeAsync(VernukoDbContext db, TicketIntakeRequest i, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == i.CustomerId, ct);
        if (customer is null || customer.State != CustomerState.Active)
        {
            return Error.Validation("ticket.customer", "Müşteri bulunamadı veya aktif değil.");
        }

        if (i.TechnicianStaffId is { } sid && !await db.Staff.AsNoTracking().AnyAsync(s => s.Id == sid, ct))
        {
            return Error.NotFound("Teknisyen");
        }

        return null;
    }

    private static async Task<long> DepositsAsync(VernukoDbContext db, ServiceTicket t, CancellationToken ct) =>
        await db.Transactions.AsNoTracking().Where(x => x.IdempotencyKey.StartsWith("ticket-deposit:") && x.CustomerId == t.CustomerId && x.Description == $"Servis {t.Number} kaporası").SumAsync(x => (long?)x.AmountMinor, ct) ?? 0;

    private const string LineKey = "IdempotencyKey";

    private static async Task<bool> AlreadyAddedAsync(VernukoDbContext db, string? key, CancellationToken ct) =>
        key is not null && await db.ServiceTicketLines.AsNoTracking().AnyAsync(l => EF.Property<string?>(l, LineKey) == "line:" + key, ct);

    private static void Remember(VernukoDbContext db, ServiceTicketLine line, string? key)
    {
        if (key is not null)
        {
            db.Entry(line).Property(LineKey).CurrentValue = "line:" + key;
        }
    }

    private void Log(VernukoDbContext db, ServiceTicket t, TicketEventKind kind, string text) =>
        db.ServiceTicketEvents.Add(ServiceTicketEvent.Create(t.Id, kind, text, UserId, UtcNow));

    private byte[]? Protect(string? code) => string.IsNullOrWhiteSpace(code) ? null : protector.Protect(Encoding.UTF8.GetBytes(code.Trim()));

    private static IntakeDetails ToIntake(TicketIntakeRequest i) =>
        new(i.CustomerId, i.Device, i.Brand, i.Model, i.SerialNumber, i.Complaint, i.Accessories, i.Condition, i.BroughtBy, i.OnSite, i.SiteAddress, i.PromisedDate, i.TechnicianStaffId);

    private static string DeviceText(ServiceTicket t) => $"{ServiceRules.DeviceName(t.Device)} {t.Brand}" + (t.Model is null ? string.Empty : $" {t.Model}");

    private async Task<List<TicketSummaryDto>> SummariesAsync(VernukoDbContext db, List<ServiceTicket> tickets, CancellationToken ct)
    {
        var (_, zone) = await BusinessContext.LoadAsync(db, ct);
        var today = TimeZones.LocalDate(UtcNow, zone);
        var customerIds = tickets.Select(t => t.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { Name = c.CompanyName ?? (c.FirstName + " " + c.LastName), c.Phone }, ct);
        var staffIds = tickets.Where(t => t.TechnicianStaffId != null).Select(t => t.TechnicianStaffId!.Value).Distinct().ToList();
        var staff = await db.Staff.AsNoTracking().Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
        return tickets.Select(t => new TicketSummaryDto(t.Id, t.Number, t.Status, customers.GetValueOrDefault(t.CustomerId)?.Name ?? "?", CustomerService.FormatPhone(customers.GetValueOrDefault(t.CustomerId)?.Phone),
            t.Device, DeviceText(t), t.SerialNumber, t.ReceivedAtUtc, t.DueDate, t.TechnicianStaffId is { } sid ? staff.GetValueOrDefault(sid) : null,
            t.IsOpen && t.DueDate < today, t.Approval == ApprovalState.Pending)).ToList();
    }

    private async Task<Result<TicketDto>> LoadAsync(VernukoDbContext db, Guid ticketId, CancellationToken ct)
    {
        var t = await db.ServiceTickets.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        if (t is null)
        {
            return Error.NotFound("Servis fişi");
        }

        var (_, zone) = await BusinessContext.LoadAsync(db, ct);
        var summary = (await SummariesAsync(db, [t], ct))[0];
        var staffIds = t.Lines.Where(l => l.StaffId != null).Select(l => l.StaffId!.Value).Distinct().ToList();
        var staff = await db.Staff.AsNoTracking().Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
        var lines = t.Lines.Where(l => !l.IsRemoved).OrderBy(l => l.LineNo)
            .Select(l => new TicketLineDto(l.Id, l.LineNo, l.Kind, l.ProductId, l.Description, l.Quantity, l.UnitPriceMinor, l.VatRate, l.TotalMinor, l.StaffId is { } s ? staff.GetValueOrDefault(s) : null)).ToList();
        var events = await db.ServiceTicketEvents.AsNoTracking().Where(e => e.TicketId == t.Id).OrderBy(e => e.AtUtc).ThenBy(e => e.Id).ToListAsync(ct);
        var userIds = events.Where(e => e.UserId != null).Select(e => e.UserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var deposits = await DepositsAsync(db, t, ct);
        var warrantyOf = t.WarrantyOfTicketId is { } wid ? await db.ServiceTickets.AsNoTracking().Where(x => x.Id == wid).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;

        WarrantyCandidateDto? candidate = null;
        if (t.IsOpen && t.SerialNumber is { } serial && t.WarrantyOfTicketId is null)
        {
            var earlier = await db.ServiceTickets.AsNoTracking()
                .Where(x => x.SerialNumber == serial && x.Id != t.Id && x.Status == TicketStatus.Delivered && x.Repaired)
                .OrderByDescending(x => x.DeliveredAtUtc).FirstOrDefaultAsync(ct);
            if (earlier?.WarrantyUntil(zone) is { } w && w.SameFault >= TimeZones.LocalDate(UtcNow, zone))
            {
                candidate = new WarrantyCandidateDto(earlier.Id, earlier.Number, TimeZones.LocalDate(earlier.DeliveredAtUtc!.Value, zone), w.SameFault, earlier.WorkDone);
            }
        }

        var warranty = t.WarrantyUntil(zone);
        var intake = new TicketIntakeRequest(t.CustomerId, t.Device, t.Brand, t.Model, t.SerialNumber, t.Complaint, t.Accessories, t.Condition, t.BroughtBy, t.OnSite, t.SiteAddress, t.DueDate, t.TechnicianStaffId);
        return new TicketDto(summary, t.CustomerId, intake, t.UnlockCodeProtected is not null, t.Diagnosis, t.WorkDone, t.EstimateMinor, t.DiagnosticFeeMinor,
            t.Approval, t.ApprovalChannel, t.ApprovalAtUtc, t.ApprovalBy, t.UnrepairableReason, t.WarrantyOfTicketId, warrantyOf, candidate,
            lines, lines.Sum(l => l.TotalMinor), t.IsOpen ? t.AmountDue(t.Status != TicketStatus.Unrepairable) : t.TotalChargedMinor, deposits,
            t.DeliveredAtUtc, t.DeliveredTo, t.Repaired, t.TotalChargedMinor, warranty?.SameFault, warranty?.Parts,
            events.Select(e => new TicketEventDto(e.AtUtc, e.Kind, e.Text, e.UserId is { } u ? users.GetValueOrDefault(u) : null)).ToList());
    }
}
