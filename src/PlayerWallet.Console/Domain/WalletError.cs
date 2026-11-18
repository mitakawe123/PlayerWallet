namespace PlayerWallet.Domain;

/// <summary>Identifies an expected wallet operation failure.</summary>
public enum WalletError
{
    /// <summary>The operation succeeded.</summary>
    None,

    /// <summary>The amount must be positive and have cent precision.</summary>
    InvalidAmount,

    /// <summary>The wallet cannot cover the operation.</summary>
    InsufficientFunds,

    /// <summary>The stake is outside the allowed limits.</summary>
    InvalidBet,

    /// <summary>The resulting balance exceeds decimal capacity.</summary>
    BalanceOverflow,

    /// <summary>The requested wallet does not exist.</summary>
    WalletNotFound,

    /// <summary>The stored wallet changed before this operation was saved.</summary>
    ConcurrencyConflict,
}
