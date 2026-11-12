using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Common;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>Ayarlar › Çoklu bilgisayar: main computer mode, connection code, connected computers.</summary>
public sealed partial class MultiComputerViewModel(IMultiComputer computer, UserSession session, IDialogService dialogs, IPlatformServices platform, TimeProvider time) : ViewModelBase
{
    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private string? _codeExpires;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<PairedComputer> Computers { get; } = [];

    public ObservableCollection<string> Addresses { get; } = [];

    public bool IsHost => computer.Role == ComputerRole.Host;

    public bool IsClient => computer.Role == ComputerRole.Client;

    public bool IsStandalone => computer.Role == ComputerRole.Standalone;

    public bool CanManage => session.Has(Permission.SettingsManage) && !IsClient;

    public Task LoadAsync() => RunAsync(async () =>
    {
        Refresh();
        Computers.Clear();
        foreach (var c in await computer.ListComputersAsync())
        {
            Computers.Add(c);
        }
    });

    [RelayCommand]
    private Task EnableHostAsync() => RunAsync(async () =>
    {
        if (!Check(await computer.EnableHostAsync()))
        {
            return;
        }

        InfoMessage = "Ana bilgisayar modu açık. Windows Güvenlik Duvarı izin sorarsa \"Özel ağlar\" için izin verin. Diğer bilgisayarda Vernuko kurulumunda \"Ana bilgisayara bağlan\" seçin ve aşağıdaki adres ile bağlantı kodunu girin.";
        await LoadAsync();
    });

    [RelayCommand]
    private Task DisableHostAsync() => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Ana bilgisayar modunu kapat", "Bağlı bilgisayarlar çalışamaz hale gelir (eşleşmeleri silinmez; modu tekrar açınca kaldıkları yerden devam ederler).", "Kapat"))
        {
            return;
        }

        if (Check(await computer.DisableHostAsync()))
        {
            Code = CodeExpires = null;
            InfoMessage = "Ana bilgisayar modu kapatıldı.";
            await LoadAsync();
        }
    });

    [RelayCommand]
    private void ShowCode()
    {
        var r = computer.NewConnectionCode();
        if (!Check(r))
        {
            return;
        }

        Code = r.Value.Code;
        CodeExpires = $"{TimeZoneInfo.ConvertTimeFromUtc(r.Value.ExpiresAtUtc, TimeZoneInfo.Local):HH:mm}'a kadar geçerli, tek kullanımlık.";
        _ = time;
    }

    [RelayCommand]
    private Task RemoveComputerAsync(PairedComputer item) => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Bilgisayarı kaldır", $"\"{item.Name}\" artık ana bilgisayara bağlanamaz; açık oturumu hemen kapanır. Yeniden bağlamak için yeni bağlantı kodu gerekir.", "Kaldır"))
        {
            return;
        }

        if (Check(await computer.RemoveComputerAsync(item.Id)))
        {
            await LoadAsync();
        }
    });

    [RelayCommand]
    private Task ForgetHostAsync() => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Ana bilgisayar bağlantısını kaldır",
                "Bu bilgisayar ana bilgisayara bağlanmayı bırakır ve Vernuko yeniden başlar. Ana bilgisayardaki verilere dokunulmaz. Bu bilgisayarı tekrar bağlamak için ana bilgisayardan yeni bir bağlantı kodu gerekir.",
                "Bağlantıyı kaldır"))
        {
            return;
        }

        if (Check(await computer.ForgetHostAsync()))
        {
            platform.RestartApplication();
        }
    });

    private void Refresh()
    {
        Addresses.Clear();
        foreach (var a in computer.Addresses)
        {
            Addresses.Add(a);
        }

        StatusText = computer.Role switch
        {
            ComputerRole.Host => $"Bu bilgisayar ana bilgisayar. {Computers.Count} bağlı bilgisayar.",
            ComputerRole.Client => "Bu bilgisayar ana bilgisayara bağlı çalışıyor. Veriler, yedekler, lisans ve otomatik mesajlar ana bilgisayarda.",
            _ => "Bu bilgisayar tek başına çalışıyor. Aynı işletmede başka bilgisayarlar da kullanılacaksa bu bilgisayarı ana bilgisayar yapın.",
        };
        OnPropertyChanged(nameof(IsHost));
        OnPropertyChanged(nameof(IsClient));
        OnPropertyChanged(nameof(IsStandalone));
        OnPropertyChanged(nameof(CanManage));
    }
}
