namespace PlayerWallet.Domain;

/// <summary>Generates gross payouts in whole cents.</summary>
public sealed class SlotGame(GameRules rules, IRandomSource random)
{
    /// <summary>Gets the rules governing this game.</summary>
    public GameRules Rules { get; } = rules ?? throw new ArgumentNullException(nameof(rules));

    private readonly IRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));

    /// <summary>Generates a payout for a stake within the game limits.</summary>
    public Money Play(Money stake)
    {
        if (stake.Amount < Rules.MinimumBet || stake.Amount > Rules.MaximumBet)
        {
            throw new ArgumentOutOfRangeException(nameof(stake));
        }

        int category = _random.NextInt(0, 100);
        if (category < Rules.LossPercent)
        {
            return default;
        }

        int cents = decimal.ToInt32(stake.Amount * 100);
        int payout = category < Rules.LossPercent + Rules.SmallWinPercent
            ? _random.NextInt(1, (2 * cents) + 1)
            : _random.NextInt((2 * cents) + 1, (10 * cents) + 1);
        return new Money(payout / 100m);
    }
}
