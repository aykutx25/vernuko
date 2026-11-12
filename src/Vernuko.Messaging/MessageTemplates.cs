using System.Text;
using System.Text.RegularExpressions;

namespace Vernuko.Messaging;

public enum MessagePurpose
{
    AppointmentCreated,
    AppointmentReminder,
    AppointmentRescheduled,
    AppointmentCancelled,
}

/// <summary>Values that can be inserted into a template. Keep it minimal (data minimisation).</summary>
public sealed record MessageContext(
    string CustomerFirstName,
    string BusinessName,
    DateTime LocalStart,
    string? ServiceName,
    string? StaffName,
    string? BusinessPhone);

/// <summary>
/// Simple, safe placeholder templates: {Ad}, {Isletme}, {Tarih}, {Saat}, {Hizmet}, {Personel}, {Telefon}.
/// Unknown placeholders are left visible so the user notices typos. For special-category businesses (health,
/// therapy) the service name is never inserted, whatever the template says.
/// </summary>
public static partial class MessageTemplates
{
    public static readonly IReadOnlyDictionary<MessagePurpose, string> Defaults = new Dictionary<MessagePurpose, string>
    {
        [MessagePurpose.AppointmentCreated] = "Merhaba {Ad}, {Tarih} {Saat} için randevunuz oluşturuldu. {Isletme}",
        [MessagePurpose.AppointmentReminder] = "Merhaba {Ad}, {Tarih} {Saat} randevunuzu hatırlatırız. Değişiklik için {Telefon}. {Isletme}",
        [MessagePurpose.AppointmentRescheduled] = "Merhaba {Ad}, randevunuz {Tarih} {Saat} olarak güncellendi. {Isletme}",
        [MessagePurpose.AppointmentCancelled] = "Merhaba {Ad}, {Tarih} {Saat} randevunuz iptal edildi. Yeni randevu için {Telefon}. {Isletme}",
    };

    public static string Render(string template, MessageContext ctx, bool hideServiceName)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(ctx);
        var culture = Core.TurkishText.Culture;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ad"] = ctx.CustomerFirstName,
            ["Isletme"] = ctx.BusinessName,
            ["İşletme"] = ctx.BusinessName,
            ["Tarih"] = ctx.LocalStart.ToString("d MMMM yyyy dddd", culture),
            ["Saat"] = ctx.LocalStart.ToString("HH:mm", culture),
            ["Hizmet"] = hideServiceName ? string.Empty : ctx.ServiceName ?? string.Empty,
            ["Personel"] = ctx.StaffName ?? string.Empty,
            ["Telefon"] = ctx.BusinessPhone ?? string.Empty,
        };

        var rendered = Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return CollapseSpaces(rendered);
    }

    private static string CollapseSpaces(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lastSpace = false;
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                if (!lastSpace)
                {
                    sb.Append(ch);
                }

                lastSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    [GeneratedRegex(@"\{([A-Za-zİıŞşĞğÜüÖöÇç]+)\}")]
    private static partial Regex Placeholder();
}
