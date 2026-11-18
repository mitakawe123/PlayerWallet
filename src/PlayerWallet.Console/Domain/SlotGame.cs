namespace PlayerWallet.Console.Domain;

/// <summary>Applies the fixed game rules and generates gross payouts in whole cents.</summary>
public static class SlotGame
{
    /// <summary>The inclusive minimum stake.</summary>
    public const decimal MinimumBet = 1m;

    /// <summary>The inclusive maximum stake.</summary>
    public const decimal MaximumBet = 10m;

    /// <summary>The percentage of draws that lose.</summary>
    public const int LossPercent = 50;

    /// <summary>The percentage of draws that win up to twice the stake.</summary>
    public const int SmallWinPercent = 40;

    /// <summary>Plays one round and returns its gross payout.</summary>
    /// <param name="stake">The stake, within the game limits.</param>
    /// <returns>The gross payout, or zero for a loss.</returns>
    public static Money Play(Money stake) => PayoutFor(Classify(Random.Shared.Next(0, 100)), stake);

    /// <summary>Selects the payout band for a draw from 0 through 99.</summary>
    /// <param name="draw">A draw from 0 through 99.</param>
    /// <returns>The band selected by the draw.</returns>
    public static PayoutBand Classify(int draw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(draw);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draw, 99);

        return draw switch
        {
            < LossPercent => PayoutBand.Loss,
            < LossPercent + SmallWinPercent => PayoutBand.SmallWin,
            _ => PayoutBand.LargeWin,
        };
    }

    /// <summary>Returns a gross payout from the band selected for the round.</summary>
    /// <param name="band">The payout band for the round.</param>
    /// <param name="stake">The stake, within the game limits.</param>
    /// <returns>The gross payout, or zero for a loss.</returns>
    public static Money PayoutFor(PayoutBand band, Money stake)
    {
        if (stake.Amount < MinimumBet || stake.Amount > MaximumBet)
        {
            throw new ArgumentOutOfRangeException(nameof(stake));
        }

        int cents = decimal.ToInt32(stake.Amount * 100);
        return band switch
        {
            PayoutBand.Loss => default,
            PayoutBand.SmallWin => new Money(Random.Shared.Next(1, (2 * cents) + 1) / 100m),
            PayoutBand.LargeWin => new Money(Random.Shared.Next((2 * cents) + 1, (10 * cents) + 1) / 100m),
            _ => throw new ArgumentOutOfRangeException(nameof(band)),
        };
    }
}
