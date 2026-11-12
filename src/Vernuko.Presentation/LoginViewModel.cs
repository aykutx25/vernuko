using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation;

public sealed partial class LoginViewModel(AuthService auth, SettingsService settings, IAppEnvironment env) : ViewModelBase
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _businessName = "Vernuko";

    public bool IsDemo => env.IsDemo;

    /// <summary>Shown under the login form so the user can see which version is running after an update.</summary>
    public string VersionText => $"Sürüm {env.AppVersion}";

    public string? DemoHint => env.IsDemo ? $"Demo modu — kullanıcı adı: {DemoDataSeeder.DemoUsername}, parola: {DemoDataSeeder.DemoPassword}" : null;

    public event EventHandler? LoggedIn;

    public async Task LoadAsync()
    {
        var info = await settings.GetBusinessInfoAsync();
        BusinessName = info?.BusinessName ?? "Vernuko";
    }

    [RelayCommand]
    private Task LoginAsync() => RunAsync(async () =>
    {
        var result = await auth.LoginAsync(Username, Password);
        Password = string.Empty;
        if (Check(result))
        {
            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
    });
}

public sealed partial class LockViewModel(AuthService auth, UserSession session) : ViewModelBase
{
    [ObservableProperty]
    private string _password = string.Empty;

    public string UserName => session.DisplayName ?? string.Empty;

    public event EventHandler? SwitchUserRequested;

    public void Refresh() => OnPropertyChanged(nameof(UserName));

    [RelayCommand]
    private Task UnlockAsync() => RunAsync(async () =>
    {
        var result = await auth.UnlockAsync(Password);
        Password = string.Empty;
        Check(result);
    });

    [RelayCommand]
    private void SwitchUser()
    {
        auth.Logout();
        SwitchUserRequested?.Invoke(this, EventArgs.Empty);
    }
}
