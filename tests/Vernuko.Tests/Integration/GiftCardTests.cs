using Microsoft.EntityFrameworkCore;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Payments;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Hediye kartı: satışta para kasaya girer, kullanımda bakiye düşer, gelir iki kez sayılmaz.</summary>
public class GiftCardTests
{
    [Fact]
    public async Task Sold_card_counts_in_the_till_once_and_paying_with_it_moves_no_money()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var buyer = await app.AddCustomerAsync("Hediye", "Alan", "0532 600 00 01");
        var recipient = await app.AddCustomerAsync("Hediye", "Kullanan", "0532 600 00 02");
        var cards = app.Get<GiftCardService>();
        var payments = app.Get<PaymentService>();
        var day = DateOnly.FromDateTime(TestApp.Local(10));

        var sold = await cards.SellAsync(new SellGiftCardRequest(buyer, 100_000, PaymentMethod.Cash, "Annem", 365, "sale-1"));
        Assert.True(sold.IsSuccess, sold.IsFailure ? sold.Error.ToString() : null);
        Assert.Matches("^HK-[A-Z2-9]{4}-[A-Z2-9]{4}$", sold.Value.Code);
        Assert.Equal(100_000, sold.Value.BalanceMinor);
        Assert.Equal(0, (await payments.GetBalanceAsync(buyer)).Value); // paid in full
        Assert.Equal(sold.Value.Id, (await cards.SellAsync(new SellGiftCardRequest(buyer, 100_000, PaymentMethod.Cash, "Annem", 365, "sale-1"))).Value.Id); // double click

        var cashAfterSale = (await payments.GetCashDayAsync(day)).Value;

        // The recipient pays 600 TL of a 700 TL service with the card, the rest in cash.
        var service = (await app.CatalogAsync()).SkinCare;
        Assert.True((await payments.RecordAsync(new RecordTransactionRequest(recipient, TransactionKind.Charge, 70_000, null, false, null, null, "Cilt bakımı", "charge-1"))).IsSuccess);
        var redeem = await cards.RedeemAsync(new RedeemGiftCardRequest(sold.Value.Code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal), recipient, 60_000, null, "redeem-1"));
        Assert.True(redeem.IsSuccess, redeem.IsFailure ? redeem.Error.ToString() : null);
        Assert.Equal(redeem.Value, (await cards.RedeemAsync(new RedeemGiftCardRequest(sold.Value.Code, recipient, 60_000, null, "redeem-1"))).Value); // retry
        Assert.Equal(40_000, (await cards.GetAsync(sold.Value.Code)).Value.BalanceMinor);
        Assert.Equal(10_000, (await payments.GetBalanceAsync(recipient)).Value);

        var cashAfterRedeem = (await payments.GetCashDayAsync(day)).Value;
        Assert.Equal(100_000, cashAfterSale.TotalNetMinor);
        Assert.Equal(cashAfterSale.TotalNetMinor, cashAfterRedeem.TotalNetMinor); // no new money from the card payment
        _ = service;

        // More than the balance is refused.
        var tooMuch = await cards.RedeemAsync(new RedeemGiftCardRequest(sold.Value.Code, recipient, 50_000, null, "redeem-2"));
        Assert.Equal("validation.giftcard.balance", tooMuch.Error.Code);

        // A gift-card payment cannot be typed in without a card (it would not lower any balance).
        var fake = await payments.RecordAsync(new RecordTransactionRequest(recipient, TransactionKind.Payment, 10_000, PaymentMethod.GiftCard, false, null, null, null, "fake-1"));
        Assert.True(fake.IsFailure);

        // Correcting the card payment puts the amount back on the card.
        Assert.True((await payments.ReverseAsync(redeem.Value, "Yanlış müşteri")).IsSuccess);
        Assert.Equal(100_000, (await cards.GetAsync(sold.Value.Code)).Value.BalanceMinor);
    }

    [Fact]
    public async Task Expired_and_voided_cards_cannot_be_used_and_the_ledger_is_append_only()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var cards = app.Get<GiftCardService>();
        var card = (await cards.SellAsync(new SellGiftCardRequest(customer, 50_000, PaymentMethod.Card, null, 30, "s"))).Value;

        app.Time.Advance(TimeSpan.FromDays(31));
        var expired = await cards.RedeemAsync(new RedeemGiftCardRequest(card.Code, customer, 1_000, null, "r1"));
        Assert.Equal("validation.giftcard.expired", expired.Error.Code);

        var second = (await cards.SellAsync(new SellGiftCardRequest(customer, 20_000, PaymentMethod.Card, null, null, "s2"))).Value;
        Assert.Null(second.ExpiresAtUtc);
        Assert.True((await cards.VoidAsync(second.Id, "Kayboldu")).IsSuccess);
        var voided = (await cards.GetAsync(second.Code)).Value;
        Assert.True(voided.IsVoided);
        Assert.Equal(0, voided.BalanceMinor);
        Assert.Equal("validation.giftcard.voided", (await cards.RedeemAsync(new RedeemGiftCardRequest(second.Code, customer, 1_000, null, "r2"))).Error.Code);
        Assert.Equal("giftcard.not_found", (await cards.RedeemAsync(new RedeemGiftCardRequest("HK-AAAA-AAAA", customer, 1_000, null, "r3"))).Error.Code);

        await using var db = await app.DbAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"GiftCardEntries\""));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"GiftCardEntries\" SET \"AmountMinor\" = 999999"));
    }
}
