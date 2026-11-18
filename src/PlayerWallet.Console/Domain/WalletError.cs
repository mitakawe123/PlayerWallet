namespace PlayerWallet.Console.Domain;

/// <summary>Identifies a wallet rule that rejected an operation.</summary>
public enum WalletError
{
    /// <summary>The amount must be positive and have cent precision.</summary>
    InvalidAmount,

    /// <summary>The wallet cannot cover the amount.</summary>
    InsufficientFunds,

    /// <summary>The stake is outside the allowed limits.</summary>
    InvalidBet,
}
