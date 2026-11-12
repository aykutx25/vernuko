using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Packages;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed partial class PackagesViewModel(IPackageService packages, IServiceProvider services, IDialogService dialogs) : PageViewModel, IShellAware
{
    private readonly Debouncer _debounce = new(TimeSpan.FromMilliseconds(250));

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _activeOnly = true;

    [ObservableProperty]
    private CustomerPackageDto? _selected;

    public override string Title => "Paket & Seanslar";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<CustomerPackageDto> Items { get; } = [];

    public ObservableCollection<SessionLedgerDto> Ledger { get; } = [];

    public bool CanAdjust => Shell?.Has(Permission.PackagesAdjustSessions) ?? false;

    public static string KindName(SessionEntryKind kind) => kind switch
    {
        SessionEntryKind.Consumed => "Seans kullanıldı",
        SessionEntryKind.Restored => "Seans iade edildi",
        _ => "Düzeltme",
    };

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = _debounce.RunAsync(_ => LoadAsync());

    partial void OnActiveOnlyChanged(bool value) => _ = LoadAsync();

    partial void OnSelectedChanged(CustomerPackageDto? value) => _ = LoadLedgerAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var id = Selected?.Id;
        var r = await packages.SearchAsync(SearchText, ActiveOnly);
        if (!Check(r))
        {
            return;
        }

        Items.Clear();
        foreach (var p in r.Value)
        {
            Items.Add(p);
        }

        Selected = Items.FirstOrDefault(p => p.Id == id);
    });

    [RelayCommand]
    private async Task SellAsync()
    {
        if (Shell is null)
        {
            return;
        }

        var dialog = services.GetRequiredService<SellPackageDialogViewModel>();
        dialog.Closed += async (_, saved) =>
        {
            Shell.CloseOverlay();
            if (saved)
            {
                await LoadAsync();
                InfoMessage = "Paket satışı kaydedildi.";
            }
        };
        await dialog.InitializeAsync(null);
        Shell.ShowOverlay(dialog);
    }

    [RelayCommand]
    private Task AdjustAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var amount = await dialogs.PromptAsync("Seans düzeltme", $"{Selected.CustomerName} — {Selected.Label}\nKalan: {Selected.RemainingSessions}\n\nEklenecek (+) veya düşülecek (−) seans sayısı:", "-1");
        if (amount is null)
        {
            return;
        }

        if (!int.TryParse(amount.Replace("+", string.Empty, StringComparison.Ordinal).Replace('−', '-'), out var delta) || delta == 0)
        {
            ErrorMessage = "Geçerli bir sayı girin (ör. -1 veya 2).";
            return;
        }

        var reason = await dialogs.PromptAsync("Düzeltme gerekçesi", "Bu değişiklik denetim kaydına yazılır. Gerekçe:");
        if (reason is null)
        {
            return;
        }

        if (Check(await packages.AdjustSessionsAsync(Selected.Id, delta, reason)))
        {
            await LoadAsync();
            InfoMessage = "Seans hakkı güncellendi.";
        }
    });

    [RelayCommand]
    private Task ToggleFreezeAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var freeze = Selected.Status == PackageStatus.Active;
        if (Check(await packages.FreezeAsync(Selected.Id, freeze)))
        {
            await LoadAsync();
            InfoMessage = freeze ? "Paket donduruldu; geçerlilik süresi dondurma bitince uzatılır." : "Paket yeniden aktif.";
        }
    });

    [RelayCommand]
    private Task ExtendAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var days = await dialogs.PromptAsync("Süre uzat", "Kaç gün uzatılsın?", "30");
        if (days is not null && int.TryParse(days, out var d) && Check(await packages.ExtendAsync(Selected.Id, d)))
        {
            await LoadAsync();
        }
    });

    [RelayCommand]
    private Task CancelPackageAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var reason = await dialogs.PromptAsync("Paketi iptal et", "Paket iptal edilecek. İade gerekiyorsa Ödemeler ekranından ayrıca kaydedin.\n\nİptal nedeni:");
        if (reason is not null && Check(await packages.CancelAsync(Selected.Id, reason)))
        {
            await LoadAsync();
        }
    });

    private async Task LoadLedgerAsync()
    {
        Ledger.Clear();
        if (Selected is null)
        {
            return;
        }

        foreach (var e in (await packages.GetLedgerAsync(Selected.Id)).OrEmpty())
        {
            Ledger.Add(e);
        }
    }
}
