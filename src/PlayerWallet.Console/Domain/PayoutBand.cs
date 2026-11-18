namespace PlayerWallet.Console.Domain;

/// <summary>Selects the payout range for a round.</summary>
public enum PayoutBand
{
    /// <summary>The round pays nothing.</summary>
    Loss,

    /// <summary>The round pays from one cent through twice the stake.</summary>
    SmallWin,

    /// <summary>The round pays above twice the stake through ten times the stake.</summary>
    LargeWin,
}
