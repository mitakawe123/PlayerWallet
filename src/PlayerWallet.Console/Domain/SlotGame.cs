namespace PlayerWallet.Domain;

/// <summary>Applies the fixed game rules and generates gross payouts in whole cents.</summary>
public static class SlotGame
{
    /// <summary>The inclusive minimum stake.</summary>
    public const decimal MinimumBet = 1m;

    /// <summary>The inclusive maximum stake.</summary>
    public const decimal MaximumBet = 10m;

    /// <summary>The percentage of rounds that pay nothing.</summary>
    public const int LossPercent = 50;

    /// <summary>The percentage of rounds that pay up to twice the stake.</summary>
    public const int SmallWinPercent = 40;

    /// <summary>Plays one round and returns its gross payout.</summary>
    /// <param name="stake">The stake, within the game limits.</param>
    /// <returns>The gross payout, or zero for a loss.</returns>
    public static Money Play(Money stake) => PayoutFor(Random.Shared.Next(0, 100), stake);

    /// <summary>Returns the gross payout for a round category and stake.</summary>
    /// <param name="category">A value from 0 through 99 that selects the outcome band.</param>
    /// <param name="stake">The stake, within the game limits.</param>
    /// <returns>The gross payout, or zero for a loss.</returns>
    public static Money PayoutFor(int category, Money stake)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(category);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(category, 99);

        if (stake.Amount < MinimumBet || stake.Amount > MaximumBet)
        {
            throw new ArgumentOutOfRangeException(nameof(stake));
        }

        if (category < LossPercent)
        {
            return default;
        }

        int cents = decimal.ToInt32(stake.Amount * 100);
        int payout = category < LossPercent + SmallWinPercent
            ? Random.Shared.Next(1, (2 * cents) + 1)
            : Random.Shared.Next((2 * cents) + 1, (10 * cents) + 1);
        return new Money(payout / 100m);
    }
}
