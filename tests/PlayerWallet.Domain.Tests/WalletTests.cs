namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies wallet money invariants and atomic settlement.</summary>
public sealed class WalletTests
{
    /// <summary>Rejects values that cannot represent nonnegative cent amounts.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void MoneyRejectsInvalidAmounts(decimal amount) => Assert.Throws<ArgumentOutOfRangeException>(() => new Money(amount));

    /// <summary>Accepts zero balances and exact cents.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1.23)]
    [InlineData(10)]
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
        Assert.Equal(WalletError.None, wallet.Withdraw(new Money(10)));
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

    /// <summary>Preserves the wallet if a deposit would overflow.</summary>
    [Fact]
    public void DepositOverflowLeavesBalanceUnchanged()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(Money.MaximumAmount));
        Assert.Equal(WalletError.BalanceOverflow, wallet.Deposit(new Money(1)));
        Assert.Equal(Money.MaximumAmount, wallet.Balance.Amount);
    }

    /// <summary>Checks inclusive bet limits before settlement.</summary>
    [Theory]
    [InlineData(0.99, WalletError.InvalidBet)]
    [InlineData(1, WalletError.None)]
    [InlineData(10, WalletError.None)]
    [InlineData(10.01, WalletError.InvalidBet)]
    public void BetLimitsAreInclusive(decimal stake, WalletError expected)
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(20));
        Assert.Equal(expected, wallet.ValidateBet(new Money(stake), new GameRules()));
        Assert.Equal(20m, wallet.Balance.Amount);
    }

    /// <summary>Uses gross payouts, including loss and payouts smaller than the stake.</summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(3, 8)]
    [InlineData(5, 10)]
    [InlineData(35.35, 40.35)]
    [InlineData(50, 55)]
    public void SettlementUsesGrossPayout(decimal payout, decimal expected)
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(10));
        Assert.Equal(WalletError.None, wallet.SettleBet(new Money(5), new Money(payout), new GameRules()));
        Assert.Equal(expected, wallet.Balance.Amount);
    }

    /// <summary>Does not deduct the stake when the final balance would overflow.</summary>
    [Fact]
    public void SettlementOverflowIsAtomic()
    {
        var wallet = new Wallet(Guid.NewGuid(), new Money(Money.MaximumAmount));
        Assert.Equal(WalletError.BalanceOverflow, wallet.SettleBet(new Money(1), new Money(10), new GameRules()));
        Assert.Equal(Money.MaximumAmount, wallet.Balance.Amount);
    }

}
