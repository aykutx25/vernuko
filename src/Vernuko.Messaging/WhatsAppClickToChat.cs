using Vernuko.Core;

namespace Vernuko.Messaging;

/// <summary>
/// Manual WhatsApp: builds an official click-to-chat link (https://wa.me/&lt;number&gt;?text=...). The user reviews
/// and sends the message themselves in WhatsApp. This is NOT automated messaging and costs nothing.
/// </summary>
public static class WhatsAppClickToChat
{
    public static Uri BuildLink(PhoneNumber phone, string message)
    {
        ArgumentNullException.ThrowIfNull(phone);
        var text = Uri.EscapeDataString(message ?? string.Empty);
        return new Uri($"https://wa.me/{phone.Digits}?text={text}");
    }
}
