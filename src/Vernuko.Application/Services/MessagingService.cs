using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Messaging;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed record ManualMessage(Uri WhatsAppLink, string Text);

/// <summary>
/// Phase 1 messaging = manual WhatsApp click-to-chat only (free; the user sends it). Automatic SMS/WhatsApp
/// adapters are Phase 5 and are not implemented — nothing here sends a message on its own.
/// </summary>
public sealed class MessagingService(AppServiceDeps deps) : AppServiceBase(deps)
{
    public Task<Result<ManualMessage>> PrepareWhatsAppAsync(Guid appointmentId, MessagePurpose purpose, CancellationToken ct = default) =>
        WriteAlwaysAsync<ManualMessage>(Permission.MessagingSend, async db =>
        {
            var (settings, zone) = await BusinessContext.LoadAsync(db, ct);
            var appt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == appt.CustomerId, ct);
            if (customer.Phone is null)
            {
                return Error.Validation("message.no_phone", "Müşterinin telefon numarası kayıtlı değil.");
            }

            if (!customer.AllowWhatsApp)
            {
                return Error.Validation("message.opted_out", "Müşteri WhatsApp ile bilgilendirme almak istemiyor.");
            }

            var service = await db.Services.AsNoTracking().Where(s => s.Id == appt.ServiceId).Select(s => s.Name).FirstAsync(ct);
            var staff = await db.Staff.AsNoTracking().Where(s => s.Id == appt.StaffMemberId).Select(s => s.DisplayName).FirstAsync(ct);
            var phone = PhoneNumber.Parse(customer.Phone).Value;
            var text = MessageTemplates.Render(
                MessageTemplates.Defaults[purpose],
                new MessageContext(customer.FirstName, settings.BusinessName, TimeZones.ToLocal(appt.StartUtc, zone), service, staff, settings.Phone),
                settings.Profile.HideServiceNameInMessages);
            await Audit.AppendAsync(db, "message.manual_whatsapp", "Appointment", appt.Id, $"Manuel WhatsApp mesajı hazırlandı ({purpose})", ct);
            return new ManualMessage(WhatsAppClickToChat.BuildLink(phone, text), text);
        }, ct);
}
