namespace PlayerWallet.Domain;

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
    public WalletError Deposit(Money amount)
    {
        if (amount.Amount is 0)
        {
            return WalletError.InvalidAmount;
        }

        return SetBalance(Balance.Amount, amount.Amount);
    }

    /// <summary>Withdraws a positive amount that the wallet can cover.</summary>
    public WalletError Withdraw(Money amount)
    {
        WalletError error = ValidateDebit(amount);
        return error is WalletError.None ? SetBalance(Balance.Amount - amount.Amount, 0) : error;
    }

    /// <summary>Checks whether the wallet can cover a positive stake within the game limits.</summary>
    public WalletError ValidateBet(Money stake)
    {
        if (stake.Amount < SlotGame.MinimumBet || stake.Amount > SlotGame.MaximumBet)
        {
            return WalletError.InvalidBet;
        }

        return ValidateDebit(stake);
    }

    /// <summary>Applies a stake and gross payout as one balance change.</summary>
    public WalletError SettleBet(Money stake, Money payout)
    {
        WalletError error = ValidateBet(stake);
        return error is WalletError.None ? SetBalance(Balance.Amount - stake.Amount, payout.Amount) : error;
    }

    private WalletError ValidateDebit(Money amount)
    {
        if (amount.Amount is 0)
        {
            return WalletError.InvalidAmount;
        }

        return amount.Amount > Balance.Amount ? WalletError.InsufficientFunds : WalletError.None;
    }

    private WalletError SetBalance(decimal remaining, decimal credit)
    {
        if (credit > Money.MaximumAmount - remaining)
        {
            return WalletError.BalanceOverflow;
        }

        Balance = new Money(remaining + credit);
        return WalletError.None;
    }
}
