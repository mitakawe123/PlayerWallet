namespace PlayerWallet.Console.Domain;

/// <summary>Protects a wallet balance from invalid money movements.</summary>
public sealed class Wallet
{
    /// <summary>Creates a wallet with its balance and last persisted version.</summary>
    public Wallet(Guid id, Money balance, long version = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        Id = id;
        Balance = balance;
        Version = version;
    }

    /// <summary>Gets the wallet identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the current balance.</summary>
    public Money Balance { get; private set; }

    /// <summary>Gets the persisted version used to reject stale saves.</summary>
    public long Version { get; }

    /// <summary>Adds a positive amount without changing state on failure.</summary>
    /// <param name="amount">The amount to add.</param>
    /// <returns>The violated rule, or null when the deposit succeeded.</returns>
    public WalletError? Deposit(Money amount)
    {
        if (amount.Amount is 0)
        {
            return WalletError.InvalidAmount;
        }

        Balance = new Money(Balance.Amount + amount.Amount);
        return null;
    }

    /// <summary>Withdraws a positive amount that the wallet can cover.</summary>
    /// <param name="amount">The amount to remove.</param>
    /// <returns>The violated rule, or null when the withdrawal succeeded.</returns>
    public WalletError? Withdraw(Money amount)
    {
        if (amount.Amount is 0)
        {
            return WalletError.InvalidAmount;
        }

        if (amount.Amount > Balance.Amount)
        {
            return WalletError.InsufficientFunds;
        }

        Balance = new Money(Balance.Amount - amount.Amount);
        return null;
    }

    /// <summary>Applies a stake and gross payout as one balance change.</summary>
    /// <param name="stake">The stake to deduct.</param>
    /// <param name="payout">The gross payout to credit.</param>
    /// <returns>The violated rule, or null when the settlement succeeded.</returns>
    /// <remarks>
    /// The wallet enforces money invariants only. Stake limits belong to the game rules and are
    /// checked by the caller before it settles, so the wallet stays usable for any game.
    /// </remarks>
    public WalletError? SettleBet(Money stake, Money payout)
    {
        if (stake.Amount > Balance.Amount)
        {
            return WalletError.InsufficientFunds;
        }

        Balance = new Money(Balance.Amount - stake.Amount + payout.Amount);
        return null;
    }
}
