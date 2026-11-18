using PlayerWallet.Console.Domain;

namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies wallet money invariants and atomic settlement.</summary>
public sealed class WalletTests
{
    /// <summary>Provides amounts that cannot represent nonnegative cent values.</summary>
    public static TheoryData<decimal> InvalidAmounts => new() { -1m, 0.001m, 0.005m };

    /// <summary>Provides amounts that are valid money values.</summary>
    public static TheoryData<decimal> ValidAmounts => new() { 0m, 0.01m, 1.23m, 10m };

    /// <summary>Provides amounts and whether they are valid money values.</summary>
    public static TheoryData<decimal, bool> MoneyValidity => new()
    {
        { 0m, true },
        { 0.01m, true },
        { 10m, true },
        { 1.005m, false },
        { -0.01m, false },
    };

    /// <summary>Provides stakes outside the inclusive game limits.</summary>
    public static TheoryData<decimal> InvalidStakes => new() { 0.99m, 10.01m };

    /// <summary>Provides stake limits and their expected validation results.</summary>
    public static TheoryData<decimal, WalletError?> BetLimits => new()
    {
        { 0.99m, WalletError.InvalidBet },
        { 1m, null },
        { 10m, null },
        { 10.01m, WalletError.InvalidBet },
    };

    /// <summary>Provides payouts and the expected gross-settlement balances.</summary>
    public static TheoryData<decimal, decimal> Settlements => new()
    {
        { 0m, 5m },
        { 0.01m, 5.01m },
        { 3m, 8m },
        { 5m, 10m },
        { 35.35m, 40.35m },
        { 50m, 55m },
    };

    /// <summary>Reports whether an amount can be stored as money.</summary>
    [Theory]
    [MemberData(nameof(MoneyValidity))]
    public void IsValidAcceptsOnlyNonnegativeCentAmounts(decimal amount, bool expected) =>
        Assert.Equal(expected, Money.IsValid(amount));

    /// <summary>Rejects values that cannot represent nonnegative cent amounts.</summary>
    [Theory]
    [MemberData(nameof(InvalidAmounts))]
    public void MoneyRejectsInvalidAmounts(decimal amount) => Assert.Throws<ArgumentOutOfRangeException>(() => new Money(amount));

    /// <summary>Accepts zero balances and exact cents.</summary>
    [Theory]
    [MemberData(nameof(ValidAmounts))]
    public void MoneyPreservesValidAmounts(decimal amount)
    {
        var money = new Money(amount);
        Assert.Equal(amount, money.Amount);
    }

    /// <summary>Rejects oversized amounts while retaining the maximum supported cent value.</summary>
    [Fact]
    public void MoneyEnforcesSupportedRange()
    {
        Assert.False(Money.IsValid(decimal.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(decimal.MaxValue));
        Assert.Equal(Money.MaximumAmount, new Money(Money.MaximumAmount).Amount);
        Assert.Equal(0m, new Money().Amount);
    }

    /// <summary>Supports exact withdrawals and rejects overdrafts without mutation.</summary>
    [Fact]
    public void WithdrawalCannotOverdraw()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(10));
        Assert.Equal(WalletError.InsufficientFunds, wallet.Withdraw(new Money(11)));
        Assert.Equal(10m, wallet.Balance.Amount);
        Assert.Null(wallet.Withdraw(new Money(10)));
        Assert.Equal(0m, wallet.Balance.Amount);
    }

    /// <summary>Rejects zero deposit and withdrawal amounts.</summary>
    [Fact]
    public void ZeroOperationsLeaveBalanceUnchanged()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(10));
        Assert.Equal(WalletError.InvalidAmount, wallet.Deposit(default));
        Assert.Equal(WalletError.InvalidAmount, wallet.Withdraw(default));
        Assert.Equal(10m, wallet.Balance.Amount);
    }

    /// <summary>Preserves the wallet when a deposit exceeds the supported range.</summary>
    [Fact]
    public void DepositOverflowLeavesBalanceUnchanged()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(Money.MaximumAmount));
        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Deposit(new Money(1)));
        Assert.Equal(Money.MaximumAmount, wallet.Balance.Amount);
    }

    /// <summary>Checks inclusive bet limits before settlement.</summary>
    [Theory]
    [MemberData(nameof(BetLimits))]
    public void BetLimitsAreInclusive(decimal stake, WalletError? expected)
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(20));
        Assert.Equal(expected, wallet.ValidateBet(new Money(stake)));
        Assert.Equal(20m, wallet.Balance.Amount);
    }

    /// <summary>Rejects a bet the balance cannot cover.</summary>
    [Fact]
    public void BetCannotExceedTheBalance()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(5));
        Assert.Equal(WalletError.InsufficientFunds, wallet.ValidateBet(new Money(10)));
        Assert.Equal(5m, wallet.Balance.Amount);
    }

    /// <summary>Uses gross payouts, including loss and payouts smaller than the stake.</summary>
    [Theory]
    [MemberData(nameof(Settlements))]
    public void SettlementUsesGrossPayout(decimal payout, decimal expected)
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(10));
        Assert.Null(wallet.SettleBet(new Money(5), new Money(payout)));
        Assert.Equal(expected, wallet.Balance.Amount);
    }

    /// <summary>Rejects settlement when the stake is outside the game limits.</summary>
    [Theory]
    [MemberData(nameof(InvalidStakes))]
    public void SettlementRejectsStakesOutsideTheLimits(decimal stake)
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(100));
        Assert.Equal(WalletError.InvalidBet, wallet.SettleBet(new Money(stake), new Money(1)));
        Assert.Equal(100m, wallet.Balance.Amount);
    }

    /// <summary>Does not deduct the stake when the final balance exceeds the supported range.</summary>
    [Fact]
    public void SettlementOverflowIsAtomic()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(Money.MaximumAmount));
        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.SettleBet(new Money(1), new Money(10)));
        Assert.Equal(Money.MaximumAmount, wallet.Balance.Amount);
    }
}
