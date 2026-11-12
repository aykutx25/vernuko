using System.Reflection;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;
using Vernuko.Presentation.Common;

namespace Vernuko.Desktop.Services;

/// <summary>
/// Velopack-based updates. The feed URL is baked in at build time (MSBuild property VernukoUpdateUrl → assembly
/// metadata) and must be HTTPS; there is no runtime override, so a local user setting cannot redirect updates.
/// Velopack verifies each package's SHA checksum from the release feed; Authenticode signing of the installer is
/// configured in the release pipeline once a code-signing certificate is available.
/// </summary>
internal sealed class VelopackUpdateService : IUpdateService
{
    private readonly UpdateManager? _manager;
    private readonly ILogger<VelopackUpdateService> _logger;
    private UpdateInfo? _found;
    private bool _downloaded;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;
        var url = typeof(VelopackUpdateService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "VernukoUpdateUrl")?.Value;
        if (string.IsNullOrWhiteSpace(url))
        {
            UnavailableReason = "Bu sürümde otomatik güncelleme kaynağı tanımlı değil. Yeni sürümü kurulum dosyasıyla yükleyebilirsiniz; verileriniz korunur.";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            UnavailableReason = "Güncelleme kaynağı geçersiz (yalnızca HTTPS kabul edilir).";
            return;
        }

        try
        {
            IUpdateSource source = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                ? new GithubSource(url, null, false)
                : new SimpleWebSource(uri);
            _manager = new UpdateManager(source);
            if (!_manager.IsInstalled)
            {
                UnavailableReason = "Vernuko kurulum paketiyle yüklenmediği için (geliştirici veya taşınabilir çalıştırma) otomatik güncelleme kapalı.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update manager could not be created");
            UnavailableReason = "Güncelleme bileşeni başlatılamadı.";
            _manager = null;
        }
    }

    public string CurrentVersion =>
        (_manager is { IsInstalled: true } ? _manager.CurrentVersion?.ToString() : null)
        ?? typeof(VelopackUpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public string? UnavailableReason { get; }

    public async Task<UpdateCheck> CheckAsync(CancellationToken ct = default)
    {
        if (_manager is null || UnavailableReason is not null)
        {
            return new UpdateCheck(false, null, UnavailableReason ?? "Güncelleme kullanılamıyor.");
        }

        try
        {
            _found = await _manager.CheckForUpdatesAsync().WaitAsync(ct);
            _downloaded = false;
            return _found is null
                ? new UpdateCheck(false, null, $"Vernuko güncel ({CurrentVersion}).")
                : new UpdateCheck(true, _found.TargetFullRelease.Version.ToString(), $"Yeni sürüm var: {_found.TargetFullRelease.Version}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Update check failed");
            return new UpdateCheck(false, null, "Güncelleme sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edin.");
        }
    }

    public async Task<bool> DownloadAsync(IProgress<int>? progress, CancellationToken ct = default)
    {
        if (_manager is null || _found is null)
        {
            return false;
        }

        try
        {
            await _manager.DownloadUpdatesAsync(_found, p => progress?.Report(p), ct);
            _downloaded = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Update download failed");
            return false;
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _found is not null && _downloaded)
        {
            _manager.ApplyUpdatesAndRestart(_found.TargetFullRelease);
        }
    }
}
