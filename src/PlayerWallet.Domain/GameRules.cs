namespace PlayerWallet.Domain;

/// <summary>Defines validated stake limits and outcome probabilities.</summary>
public sealed class GameRules
{
    private const string InvalidLimitsMessage = "Bet limits must be positive cents, ordered, and at most 1,000,000 dollars.";
    private const string InvalidProbabilitiesMessage = "Outcome percentages must be nonnegative and total at most 100.";

    /// <summary>Creates game rules with cent-based limits and percentage probabilities.</summary>
    public GameRules(decimal minimumBet = 1m, decimal maximumBet = 10m, int lossPercent = 50, int smallWinPercent = 40)
    {
        if (!Money.IsValid(minimumBet) || !Money.IsValid(maximumBet)
            || minimumBet <= 0 || maximumBet < minimumBet || maximumBet > 1_000_000m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumBet), InvalidLimitsMessage);
        }

        if (lossPercent < 0 || smallWinPercent < 0 || lossPercent > 100 || smallWinPercent > 100 - lossPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(lossPercent), InvalidProbabilitiesMessage);
        }

        MinimumBet = minimumBet;
        MaximumBet = maximumBet;
        LossPercent = lossPercent;
        SmallWinPercent = smallWinPercent;
    }

    /// <summary>Gets the inclusive minimum stake.</summary>
    public decimal MinimumBet { get; }

    /// <summary>Gets the inclusive maximum stake.</summary>
    public decimal MaximumBet { get; }

    /// <summary>Gets the probability percentage for a zero payout.</summary>
    public int LossPercent { get; }

    /// <summary>Gets the probability percentage for a payout up to twice the stake.</summary>
    public int SmallWinPercent { get; }
}
