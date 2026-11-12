using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Licensing;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>
/// Talks to the Vernuko license server: activation with a license key, periodic refresh (about weekly), freeing
/// the seat when moving to a new computer, and the server-side trial record. Only the license key, the hashed
/// device id and the activation id are sent — never business or customer data. Network problems never lock the
/// app: the signed document keeps working for its offline grace period.
/// </summary>
public sealed class LicenseOnlineService(LicenseService license, LicensingOptions options, IDeviceIdentity device, UserSession session, TimeProvider time) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Lazy<HttpClient> _http = new(() => new HttpClient
    {
        BaseAddress = options.ServerUrl is { } u && !u.AbsoluteUri.EndsWith('/') ? new Uri(u.AbsoluteUri + "/") : options.ServerUrl,
        Timeout = TimeSpan.FromSeconds(20),
    });
    private DateTime _lastAutoAttemptUtc = DateTime.MinValue;

    /// <summary>Null when online licensing can be used; otherwise why not.</summary>
    public string? UnavailableReason => options.ServerUrl switch
    {
        null => "Bu sürümde lisans sunucusu tanımlı değil. Lisans dosyasıyla etkinleştirebilirsiniz.",
        { Scheme: "https" } => null,
        { IsLoopback: true, Scheme: "http" } => null,
        _ => "Lisans sunucusu adresi geçersiz (yalnızca HTTPS).",
    };

    public bool IsAvailable => UnavailableReason is null;

    public async Task<Result<LicenseStatus>> ActivateAsync(string licenseKey, CancellationToken ct = default)
    {
        if (!session.Has(Permission.LicenseManage))
        {
            return Error.Forbidden;
        }

        if (UnavailableReason is { } reason)
        {
            return new Error("license.offline", reason);
        }

        var response = await PostAsync<LicenseReply>("v1/activate", new { licenseKey, deviceHash = device.DeviceHash }, ct);
        return response.IsFailure ? response.Error : await license.StoreFromServerAsync(response.Value.License, activation: true, ct);
    }

    /// <summary>Gets a fresh document for the current activation. Not needed (success, no call) without one.</summary>
    public async Task<Result<LicenseStatus>> RefreshAsync(CancellationToken ct = default)
    {
        if (UnavailableReason is { } reason)
        {
            return new Error("license.offline", reason);
        }

        var doc = await license.GetDocumentAsync(ct);
        if (doc is null)
        {
            return license.Current;
        }

        var response = await PostAsync<LicenseReply>("v1/refresh", new { activationId = doc.ActivationId, deviceHash = device.DeviceHash }, ct);
        return response.IsFailure ? response.Error : await license.StoreFromServerAsync(response.Value.License, activation: false, ct);
    }

    /// <summary>
    /// Background check-in: at most every 6 hours, and only when the document is at least a day old (or the
    /// refresh time is near). Failures are silent here; the status message shows the offline grace days.
    /// </summary>
    public async Task AutoRefreshAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (!IsAvailable || now - _lastAutoAttemptUtc < TimeSpan.FromHours(6))
        {
            return;
        }

        _lastAutoAttemptUtc = now;
        var doc = await license.GetDocumentAsync(ct);
        if (doc is null)
        {
            if (license.Current.State is LicenseState.Trial or LicenseState.TrialExpired)
            {
                await SyncTrialAsync(ct);
            }

            return;
        }

        if (now - doc.IssuedAtUtc >= TimeSpan.FromDays(1) || now >= doc.RefreshAfterUtc.AddDays(-2))
        {
            _ = await RefreshAsync(ct);
        }
    }

    /// <summary>Frees this computer's seat on the server, then removes the local license (data is kept).</summary>
    public async Task<Result<LicenseStatus>> DeactivateAsync(CancellationToken ct = default)
    {
        if (!session.Has(Permission.LicenseManage))
        {
            return Error.Forbidden;
        }

        if (UnavailableReason is { } reason)
        {
            return new Error("license.offline", reason);
        }

        var doc = await license.GetDocumentAsync(ct);
        if (doc is null)
        {
            return new Error("license.none", "Bu bilgisayarda etkin bir lisans yok.");
        }

        var response = await PostAsync<MessageReply>("v1/deactivate", new { activationId = doc.ActivationId, deviceHash = device.DeviceHash }, ct);
        if (response.IsFailure && response.Error.Code != "activation.removed")
        {
            return response.Error;
        }

        return await license.RemoveLocalAsync($"Lisans {doc.LicenseId} bu bilgisayardan kaldırıldı (yeni bilgisayara taşıma)", ct);
    }

    public async Task<Result<LicenseStatus>> SyncTrialAsync(CancellationToken ct = default)
    {
        if (UnavailableReason is { } reason)
        {
            return new Error("license.offline", reason);
        }

        var response = await PostAsync<TrialReply>("v1/trial", new { deviceHash = device.DeviceHash }, ct);
        return response.IsFailure ? response.Error : await license.AdoptServerTrialStartAsync(response.Value.StartedAtUtc, ct);
    }

    public void Dispose()
    {
        if (_http.IsValueCreated)
        {
            _http.Value.Dispose();
        }
    }

    private async Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.Value.PostAsJsonAsync(path, body, Json, ct);
        }
        catch (HttpRequestException)
        {
            return new Error("license.offline", "Lisans sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edin; lisansınız çevrimdışı süre boyunca çalışmaya devam eder.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Error("license.offline", "Lisans sunucusu zamanında yanıt vermedi. Daha sonra tekrar denenecek.");
        }

        using (response)
        {
            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                    return value is null ? new Error("license.server", "Lisans sunucusu boş yanıt verdi.") : value;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return new Error("license.rate_limited", "Çok fazla deneme yapıldı. Bir dakika sonra tekrar deneyin.");
                }

                var error = await response.Content.ReadFromJsonAsync<ErrorReply>(Json, ct);
                return error is { Code: { } code, Message: { } message }
                    ? new Error(code, message)
                    : new Error("license.server", $"Lisans sunucusu hata verdi (HTTP {(int)response.StatusCode}).");
            }
            catch (JsonException)
            {
                return new Error("license.server", $"Lisans sunucusunun yanıtı okunamadı (HTTP {(int)response.StatusCode}).");
            }
        }
    }

    private sealed record LicenseReply(string License, string ActivationId);

    private sealed record MessageReply(string? Message);

    private sealed record TrialReply(DateTime StartedAtUtc, int TrialDays);

    private sealed record ErrorReply(string? Code, string? Message);
}
