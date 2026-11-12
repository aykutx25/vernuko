using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Payments;

namespace Vernuko.Tests.Domain;

public class PackageAndPaymentTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 7, 0, 0, DateTimeKind.Utc);

    private static CustomerPackage Laser8() =>
        CustomerPackage.Sell(Guid.NewGuid(), Guid.NewGuid(), null, "Lazer 8 Seans", "Bacak", 8, 800_000, 365, Now).Value;

    [Fact]
    public void Consumption_is_idempotent_per_appointment_and_restorable()
    {
        var p = Laser8();
        var appt = Guid.NewGuid();
        Assert.True(p.ConsumeFor(appt, null, Now).IsSuccess);
        Assert.True(p.ConsumeFor(appt, null, Now).IsSuccess);
        Assert.Equal(7, p.RemainingSessions);
        Assert.Equal(1, p.UsedSessions);

        Assert.True(p.RestoreFor(appt, null, Now, "geri alındı").IsSuccess);
        Assert.True(p.RestoreFor(appt, null, Now, "tekrar").IsSuccess);
        Assert.Equal(8, p.RemainingSessions);
        Assert.Equal(0, p.UsedSessions);
        Assert.Equal(2, p.Entries.Count);
    }

    [Fact]
    public void Adjustments_need_reason_and_cannot_go_negative()
    {
        var p = Laser8();
        Assert.True(p.Adjust(-3, " ", null, Now).IsFailure);
        Assert.True(p.Adjust(-9, "fazla", null, Now).IsFailure);
        Assert.True(p.Adjust(-3, "Önceki sistemden devreden kullanım", null, Now).IsSuccess);
        Assert.Equal(5, p.RemainingSessions);
    }

    [Fact]
    public void Exhausted_frozen_or_expired_packages_cannot_be_booked()
    {
        var p = CustomerPackage.Sell(Guid.NewGuid(), Guid.NewGuid(), null, "1 Seans", null, 1, 0, 30, Now).Value;
        Assert.True(p.CanBook(Now).IsSuccess);
        Assert.True(p.CanBook(Now.AddDays(31)).IsFailure);
        Assert.True(p.Freeze(Now).IsSuccess);
        Assert.True(p.CanBook(Now).IsFailure);
        Assert.True(p.Unfreeze(Now.AddDays(10)).IsSuccess);
        Assert.Equal(Now.AddDays(40), p.ExpiresAtUtc);
        p.ConsumeFor(Guid.NewGuid(), null, Now);
        Assert.True(p.CanBook(Now).IsFailure);
        Assert.True(p.ConsumeFor(Guid.NewGuid(), null, Now).IsFailure);
    }

    [Fact]
    public void Ledger_balance_and_reversals()
    {
        var customer = Guid.NewGuid();
        CustomerTransaction Tx(TransactionKind kind, long amount, PaymentMethod? m = null) =>
            CustomerTransaction.Create(Guid.Empty, customer, kind, new Money(amount), m, false, null, null, null, null, Now, Guid.NewGuid().ToString()).Value;

        var charge = Tx(TransactionKind.Charge, 100_000);
        var pay = Tx(TransactionKind.Payment, 60_000, PaymentMethod.Cash);
        var discount = Tx(TransactionKind.Discount, 10_000);
        var list = new List<CustomerTransaction> { charge, pay, discount };
        Assert.Equal(30_000, AccountMath.Balance(list));

        var reversal = CustomerTransaction.Reverse(pay, "Yanlış tutar", null, Now).Value;
        list.Add(reversal);
        Assert.Equal(90_000, AccountMath.Balance(list));
        Assert.Equal(0, AccountMath.CashByMethod(list)[PaymentMethod.Cash]);
        Assert.True(CustomerTransaction.Reverse(reversal, "x", null, Now).IsFailure);
    }

    [Fact]
    public void Payment_requires_method_and_positive_amount()
    {
        Assert.True(CustomerTransaction.Create(Guid.Empty, Guid.NewGuid(), TransactionKind.Payment, new Money(100), null, false, null, null, null, null, Now, "k1").IsFailure);
        Assert.True(CustomerTransaction.Create(Guid.Empty, Guid.NewGuid(), TransactionKind.Payment, new Money(0), PaymentMethod.Cash, false, null, null, null, null, Now, "k2").IsFailure);
    }
}
