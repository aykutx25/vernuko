using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;

namespace Vernuko.Presentation.Pages;

/// <summary>Ödemeler › Hediye kartları: satış, kod sorgulama, iptal. Kullanım ödeme penceresinden yapılır.</summary>
public sealed partial class GiftCardsViewModel(IGiftCardService giftCards, ICustomerService customers, IDialogService dialogs) : ViewModelBase
{
    private string _idempotencyKey = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _buyerSearch = string.Empty;

    [ObservableProperty]
    private string _amount = string.Empty;

    [ObservableProperty]
    private Option<PaymentMethod> _method = PaymentDialogViewModel.MoneyMethods[0];

    [ObservableProperty]
    private string? _recipientName;

    [ObservableProperty]
    private string _validDays = "365";

    [ObservableProperty]
    private string _lookupCode = string.Empty;

    [ObservableProperty]
    private GiftCardDto? _found;

    public IReadOnlyList<Option<PaymentMethod>> Methods => PaymentDialogViewModel.MoneyMethods;

    public ObservableCollection<GiftCardDto> Active { get; } = [];

    public Task LoadAsync() => RunAsync(async () =>
    {
        Active.Clear();
        foreach (var c in (await giftCards.ListAsync()).OrEmpty())
        {
            Active.Add(c);
        }
    });

    [RelayCommand]
    private Task SellAsync() => RunAsync(async () =>
    {
        var (buyer, error) = await ResultExtensions.ResolveTypedCustomerAsync(customers, BuyerSearch);
        if (buyer is null)
        {
            ErrorMessage = error;
            return;
        }

        var money = Money.Parse(Amount);
        if (!Check(money))
        {
            return;
        }

        int? days = string.IsNullOrWhiteSpace(ValidDays) ? null : int.TryParse(ValidDays, out var d) ? d : -1;
        var sold = await giftCards.SellAsync(new SellGiftCardRequest(buyer.Id, money.Value.Minor, Method.Value, RecipientName, days, _idempotencyKey));
        if (!Check(sold))
        {
            return;
        }

        _idempotencyKey = Guid.NewGuid().ToString("N");
        Found = sold.Value;
        LookupCode = sold.Value.Code;
        Amount = string.Empty;
        RecipientName = null;
        InfoMessage = $"Hediye kartı satıldı: {sold.Value.Code} ({Formatting.Money(sold.Value.InitialMinor)}). Kodu karta yazın veya alıcıya iletin.";
        await LoadAsync();
    });

    [RelayCommand]
    private Task LookupAsync() => RunAsync(async () =>
    {
        var r = await giftCards.GetAsync(LookupCode);
        Found = Check(r) ? r.Value : null;
    });

    [RelayCommand]
    private Task VoidAsync() => RunAsync(async () =>
    {
        if (Found is not { } card)
        {
            return;
        }

        var reason = await dialogs.PromptAsync("Hediye kartını iptal et", $"{card.Code} iptal edilecek; kalan {Formatting.Money(card.BalanceMinor)} kullanılamaz hale gelir. Para iadesi yapacaksanız ödeme ekranından ayrıca iade kaydı girin. Gerekçe:");
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        if (Check(await giftCards.VoidAsync(card.Id, reason)))
        {
            await LookupAsync();
            await LoadAsync();
        }
    });
}
