namespace PlayerWallet.Console.Domain;

/// <summary>Protects a wallet balance from invalid money movements.</summary>
public sealed class Wallet
{
    /// <summary>Creates a wallet with its identifier and starting balance.</summary>
    public Wallet(Guid id, Money balance)
    {
        Id = id;
        Balance = balance;
    }

    /// <summary>Gets the wallet identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the current balance.</summary>
    public Money Balance { get; private set; }

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

    /// <summary>Checks whether the wallet can cover a positive stake within the game limits.</summary>
    /// <param name="stake">The stake to check.</param>
    /// <returns>The violated rule, or null when the stake is playable.</returns>
    public WalletError? ValidateBet(Money stake)
    {
        if (!SlotGame.IsStakeWithinLimits(stake.Amount))
        {
            return WalletError.InvalidBet;
        }

        return stake.Amount > Balance.Amount ? WalletError.InsufficientFunds : null;
    }

    /// <summary>Applies a stake and gross payout as one balance change.</summary>
    /// <param name="stake">The stake to deduct.</param>
    /// <param name="payout">The gross payout to credit.</param>
    /// <returns>The violated rule, or null when the settlement succeeded.</returns>
    public WalletError? SettleBet(Money stake, Money payout)
    {
        if (ValidateBet(stake) is { } error)
        {
            return error;
        }

        Balance = new Money(Balance.Amount - stake.Amount + payout.Amount);
        return null;
    }
}
