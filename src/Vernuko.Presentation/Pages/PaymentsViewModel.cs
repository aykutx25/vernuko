using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Payments;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed record MethodTotal(string Method, string Amount);

/// <summary>Daily cash desk, corrections and open balances. Not an invoicing system.</summary>
public sealed partial class PaymentsViewModel(IPaymentService payments, ICustomerService customers, IServiceProvider services, IDialogService dialogs, TimeProvider time, GiftCardsViewModel giftCards) : PageViewModel, IShellAware
{
    [ObservableProperty]
    private DateTime _date = time.GetLocalNow().Date;

    [ObservableProperty]
    private string _totalText = string.Empty;

    [ObservableProperty]
    private TransactionDto? _selected;

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _paymentCustomer;

    public override string Title => "Ödemeler";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<MethodTotal> Totals { get; } = [];

    /// <summary>SPA / güzellik: hediye kartı satışı ve sorgulama.</summary>
    public GiftCardsViewModel GiftCards { get; } = giftCards;

    public bool ShowGiftCards => Shell?.Business?.Features.Contains(BusinessProfiles.ProfileFeature.GiftCards) == true && (Shell?.Has(Security.Permission.PaymentsRecord) ?? false);

    public ObservableCollection<TransactionDto> Transactions { get; } = [];

    public ObservableCollection<DebtorDto> Debtors { get; } = [];

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public bool CanSeeDebtors => Shell?.Has(Permission.ReportsFinancial) ?? false;

    public bool CanReverse => Shell?.Has(Permission.PaymentsReverse) ?? false;

    public string Disclaimer => "Bu ekran tahsilat kayıtlarını tutar; resmî fatura/e-Arşiv belgesi düzenlemez.";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        await LoadAsync();
        OnPropertyChanged(nameof(ShowGiftCards));
        if (ShowGiftCards)
        {
            await GiftCards.LoadAsync();
        }
    }

    partial void OnDateChanged(DateTime value) => _ = LoadAsync();

    partial void OnCustomerSearchChanged(string value)
    {
        if (PaymentCustomer?.FullName == value)
        {
            return;
        }

        _ = SearchCustomersAsync(value);
    }

    partial void OnPaymentCustomerChanged(CustomerListItem? value)
    {
        if (value is null || Shell is null)
        {
            return;
        }

        CustomerSearch = value.FullName;
        CustomerResults.Clear();
        var dialog = services.GetRequiredService<PaymentDialogViewModel>();
        dialog.Closed += async (_, saved) =>
        {
            Shell.CloseOverlay();
            PaymentCustomer = null;
            CustomerSearch = string.Empty;
            if (saved)
            {
                await LoadAsync();
                InfoMessage = "Ödeme kaydedildi.";
            }
        };
        dialog.Initialize(value.Id, value.FullName, null, 0);
        Shell.ShowOverlay(dialog);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var day = await payments.GetCashDayAsync(DateOnly.FromDateTime(Date));
        if (!Check(day))
        {
            return;
        }

        Totals.Clear();
        foreach (var (method, amount) in day.Value.NetByMethod)
        {
            Totals.Add(new MethodTotal(CustomerTransaction.MethodName(method), Formatting.Money(amount)));
        }

        TotalText = Formatting.Money(day.Value.TotalNetMinor);
        Transactions.Clear();
        foreach (var t in day.Value.Transactions)
        {
            Transactions.Add(t);
        }

        Debtors.Clear();
        if (CanSeeDebtors)
        {
            foreach (var d in (await payments.GetDebtorsAsync()).OrEmpty())
            {
                Debtors.Add(d);
            }
        }
    });

    [RelayCommand]
    private Task ReverseAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        if (Selected.IsReversed || Selected.Kind == TransactionKind.Reversal)
        {
            ErrorMessage = "Bu kayıt zaten düzeltilmiş veya bir düzeltme kaydı.";
            return;
        }

        var reason = await dialogs.PromptAsync("Kaydı düzelt", $"{Selected.KindName} {Formatting.Money(Selected.AmountMinor)} — {Selected.CustomerName}\nKayıt silinmez; karşılığında ters kayıt oluşturulur.\n\nGerekçe:");
        if (reason is not null && Check(await payments.ReverseAsync(Selected.Id, reason)))
        {
            await LoadAsync();
            InfoMessage = "Ters kayıt oluşturuldu.";
        }
    });

    private async Task SearchCustomersAsync(string text)
    {
        CustomerResults.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var c in (await customers.SearchAsync(text, take: 6)).OrEmpty())
        {
            CustomerResults.Add(c);
        }
    }
}
