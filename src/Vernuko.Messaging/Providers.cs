using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vernuko.Core;

namespace Vernuko.Messaging;

/// <summary>Result of handing a message to a provider. "Accepted" is not "delivered".</summary>
public sealed record SendOutcome(bool Accepted, string? ProviderMessageId, string? ErrorCode, string? ErrorMessage, bool IsRetryable)
{
    public static SendOutcome Ok(string? id) => new(true, id, null, null, false);

    public static SendOutcome Fail(string code, string message, bool retryable) => new(false, null, code, message, retryable);
}

public interface IMessageTransport
{
    string ProviderKey { get; }

    MessageChannel Channel { get; }

    Task<SendOutcome> SendAsync(PhoneNumber to, string text, IReadOnlyList<string> templateParameters, bool isCommercial, CancellationToken ct);
}

/// <summary>
/// İleti Merkezi REST API (https://www.iletimerkezi.com/en/docs/api/send-sms.md, read 2026-11-12):
/// POST https://api.iletimerkezi.com/v1/send-sms/json, credentials (key + hash) in the body, recipients under the
/// intentionally misspelled "receipents". The business uses its own account and approved sender name.
/// Implemented from the documentation and tested against the documented request/response shapes; not yet run
/// against a live account.
/// </summary>
public sealed class IletiMerkeziSmsTransport(HttpClient http, string key, string hash, string sender) : IMessageTransport
{
    public const string Key = "iletimerkezi";
    public static readonly Uri SendUri = new("https://api.iletimerkezi.com/v1/send-sms/json");

    public string ProviderKey => Key;

    public MessageChannel Channel => MessageChannel.Sms;

    public async Task<SendOutcome> SendAsync(PhoneNumber to, string text, IReadOnlyList<string> templateParameters, bool isCommercial, CancellationToken ct)
    {
        var order = new JsonObject
        {
            ["sender"] = sender,
            ["iys"] = isCommercial ? "1" : "0",
            ["message"] = new JsonObject
            {
                ["text"] = text,
                ["receipents"] = new JsonObject { ["number"] = new JsonArray(to.E164) },
            },
        };
        if (isCommercial)
        {
            order["iysList"] = "BIREYSEL";
        }

        var body = new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["authentication"] = new JsonObject { ["key"] = key, ["hash"] = hash },
                ["order"] = order,
            },
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(SendUri, body, ct);
        }
        catch (HttpRequestException ex)
        {
            return SendOutcome.Fail("network", "SMS sağlayıcısına ulaşılamadı: " + ex.Message, retryable: true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SendOutcome.Fail("timeout", "SMS sağlayıcısı zamanında yanıt vermedi.", retryable: true);
        }

        using (response)
        {
            var json = await ReadJsonAsync(response, ct);
            var code = json?["response"]?["status"]?["code"]?.GetValue<int>();
            var message = json?["response"]?["status"]?["message"]?.GetValue<string>();
            if (code == 200)
            {
                return SendOutcome.Ok(json?["response"]?["order"]?["id"]?.ToString());
            }

            if (code is null)
            {
                return SendOutcome.Fail(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), $"SMS sağlayıcısı beklenmeyen yanıt verdi (HTTP {(int)response.StatusCode}).", retryable: (int)response.StatusCode >= 500);
            }

            var (text2, retry) = Describe(code.Value, message);
            return SendOutcome.Fail(code.Value.ToString(CultureInfo.InvariantCulture), text2, retry);
        }
    }

    /// <summary>Codes from https://www.iletimerkezi.com/en/docs/api/error-codes.md.</summary>
    public static (string Message, bool Retryable) Describe(int code, string? providerMessage) => code switch
    {
        401 => ("İleti Merkezi kimlik bilgileri hatalı veya panelde \"API kullanımına izin ver\" kapalı.", false),
        402 => ("İleti Merkezi bakiyesi yetersiz.", false),
        450 => ("Gönderici başlığı (sender) onaylı değil.", false),
        452 or 466 => ("Alıcı numarası geçersiz.", false),
        454 => ("Mesaj metni boş.", false),
        459 or 460 => ("Gönderici başlığı boş, 11 karakterden uzun veya Türkçe karakter içeriyor.", false),
        468 or 469 or 470 or 403 => ("İYS bilgisi eksik veya İYS aboneliği yok.", false),
        405 => ("İleti Merkezi kotası aşıldı.", true),
        503 => ("İleti Merkezi hizmeti geçici olarak kullanılamıyor.", true),
        _ => ($"İleti Merkezi hatası {code}: {providerMessage}", code >= 500),
    };

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// WhatsApp Business Cloud API template message
/// (https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/overview, read 2026-11-12):
/// POST https://graph.facebook.com/{version}/{phone-number-id}/messages with a Bearer token. Reminders must use an
/// APPROVED utility template whose body has positional parameters {{1}} first name, {{2}} date, {{3}} time,
/// {{4}} business name. The business's own Meta account and token are used. Not yet run against a live account.
/// </summary>
public sealed class WhatsAppCloudTransport(HttpClient http, string apiVersion, string phoneNumberId, string accessToken, string templateName, string languageCode) : IMessageTransport
{
    public const string Key = "whatsapp-cloud";

    public string ProviderKey => Key;

    public MessageChannel Channel => MessageChannel.WhatsApp;

    public async Task<SendOutcome> SendAsync(PhoneNumber to, string text, IReadOnlyList<string> templateParameters, bool isCommercial, CancellationToken ct)
    {
        var parameters = new JsonArray();
        foreach (var p in templateParameters)
        {
            parameters.Add(new JsonObject { ["type"] = "text", ["text"] = p });
        }

        var body = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = to.E164,
            ["type"] = "template",
            ["template"] = new JsonObject
            {
                ["name"] = templateName,
                ["language"] = new JsonObject { ["code"] = languageCode },
                ["components"] = new JsonArray(new JsonObject { ["type"] = "body", ["parameters"] = parameters }),
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://graph.facebook.com/{Uri.EscapeDataString(apiVersion)}/{Uri.EscapeDataString(phoneNumberId)}/messages"))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return SendOutcome.Fail("network", "WhatsApp sunucusuna ulaşılamadı: " + ex.Message, retryable: true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SendOutcome.Fail("timeout", "WhatsApp sunucusu zamanında yanıt vermedi.", retryable: true);
        }

        using (response)
        {
            JsonNode? json;
            try
            {
                json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (JsonException)
            {
                json = null;
            }

            var id = json?["messages"]?[0]?["id"]?.GetValue<string>();
            if (response.IsSuccessStatusCode && id is not null)
            {
                return SendOutcome.Ok(id);
            }

            var code = json?["error"]?["code"]?.GetValue<int>();
            if (code is null)
            {
                return SendOutcome.Fail(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), $"WhatsApp beklenmeyen yanıt verdi (HTTP {(int)response.StatusCode}).",
                    retryable: response.StatusCode >= HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.TooManyRequests);
            }

            var (message, retry) = Describe(code.Value, json?["error"]?["message"]?.GetValue<string>());
            return SendOutcome.Fail(code.Value.ToString(CultureInfo.InvariantCulture), message, retry);
        }
    }

    /// <summary>Codes from https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes.</summary>
    public static (string Message, bool Retryable) Describe(int code, string? providerMessage) => code switch
    {
        131026 => ("Numara WhatsApp kullanmıyor veya mesaj teslim edilemiyor.", false),
        131009 or 131008 or 100 or 132000 or 132012 => ("WhatsApp şablon parametreleri hatalı.", false),
        132001 => ("WhatsApp şablonu bu dilde yok veya henüz onaylanmamış.", false),
        132007 or 132015 or 132016 => ("WhatsApp şablonu duraklatılmış veya devre dışı.", false),
        131047 => ("24 saatlik pencere kapalı; onaylı şablon gerekli.", false),
        190 or 0 or 200 => ("WhatsApp erişim anahtarı (token) geçersiz veya süresi dolmuş.", false),
        3 or 10 => ("WhatsApp hesabında gerekli izin yok.", false),
        368 => ("WhatsApp işletme hesabı kısıtlanmış.", false),
        130403 => ("Müşteri işletmeyi WhatsApp'ta engellemiş.", false),
        131021 => ("Gönderen ve alıcı numarası aynı.", false),
        130429 or 4 or 80007 or 131056 => ("WhatsApp gönderim hız sınırı; daha sonra tekrar denenecek.", true),
        _ => ($"WhatsApp hatası {code}: {providerMessage}", false),
    };
}
