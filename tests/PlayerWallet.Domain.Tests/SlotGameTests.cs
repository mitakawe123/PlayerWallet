using PlayerWallet.Console.Domain;

namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies the fixed game rules, category boundaries, and payout bands.</summary>
public sealed class SlotGameTests
{
    private static readonly Money Stake = new(5m);

    /// <summary>Provides stakes outside the inclusive game limits.</summary>
    public static TheoryData<decimal> InvalidStakes => new() { 0.99m, 10.01m };

    /// <summary>Checks the exact band boundaries.</summary>
    [Theory]
    [InlineData(0, PayoutBand.Loss)]
    [InlineData(49, PayoutBand.Loss)]
    [InlineData(50, PayoutBand.SmallWin)]
    [InlineData(89, PayoutBand.SmallWin)]
    [InlineData(90, PayoutBand.LargeWin)]
    [InlineData(99, PayoutBand.LargeWin)]
    public void ClassifyUsesTheDocumentedBoundaries(int draw, PayoutBand expected) =>
        Assert.Equal(expected, SlotGame.Classify(draw));

    /// <summary>Maps every possible draw to one of the three bands.</summary>
    [Fact]
    public void ClassifyCoversEveryDraw()
    {
        for (int draw = 0; draw < 100; draw++)
        {
            PayoutBand expected = draw switch
            {
                < 50 => PayoutBand.Loss,
                < 90 => PayoutBand.SmallWin,
                _ => PayoutBand.LargeWin,
            };
            Assert.Equal(expected, SlotGame.Classify(draw));
        }
    }

    /// <summary>Rejects draws outside the 0 through 99 range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void ClassifyRejectsDrawsOutsideTheRange(int draw) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.Classify(draw));

    /// <summary>Pays nothing for a loss.</summary>
    [Fact]
    public void LossPaysNothing() => Assert.Equal(0m, SlotGame.PayoutFor(PayoutBand.Loss, Stake).Amount);

    /// <summary>Pays from one cent through twice the stake for a small win.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void SmallWinsPayFromOneCentThroughTwiceTheStake(decimal stake) =>
        Assert.InRange(SlotGame.PayoutFor(PayoutBand.SmallWin, new Money(stake)).Amount, 0.01m, 2 * stake);

    /// <summary>Pays above twice the stake through ten times the stake for a large win.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void LargeWinsPayAboveTwiceThroughTenTimesTheStake(decimal stake) =>
        Assert.InRange(SlotGame.PayoutFor(PayoutBand.LargeWin, new Money(stake)).Amount, (2 * stake) + 0.01m, 10 * stake);

    /// <summary>Keeps repeated draws inside the selected band.</summary>
    [Theory]
    [InlineData(PayoutBand.SmallWin)]
    [InlineData(PayoutBand.LargeWin)]
    public void RepeatedDrawsStayInsideTheBand(PayoutBand band)
    {
        decimal minimum = band is PayoutBand.SmallWin ? 0.01m : (2 * Stake.Amount) + 0.01m;
        decimal maximum = band is PayoutBand.SmallWin ? 2 * Stake.Amount : 10 * Stake.Amount;
        for (int draw = 0; draw < 500; draw++)
        {
            Assert.InRange(SlotGame.PayoutFor(band, Stake).Amount, minimum, maximum);
        }
    }

    /// <summary>Rejects stakes outside the inclusive game limits.</summary>
    [Theory]
    [MemberData(nameof(InvalidStakes))]
    public void PayoutForRejectsStakesOutsideTheLimits(decimal stake) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.PayoutFor(PayoutBand.Loss, new Money(stake)));

    /// <summary>Rejects an undefined payout band.</summary>
    [Fact]
    public void PayoutForRejectsUnknownBands() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.PayoutFor((PayoutBand)99, Stake));

    /// <summary>Accepts the inclusive stake limits and stays within the maximum payout.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void PlayStaysWithinTheMaximumPayout(decimal stake) =>
        Assert.InRange(SlotGame.Play(new Money(stake)).Amount, 0m, 10 * stake);

    /// <summary>Rejects stakes outside the inclusive game limits.</summary>
    [Theory]
    [MemberData(nameof(InvalidStakes))]
    public void PlayRejectsStakesOutsideTheLimits(decimal stake) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.Play(new Money(stake)));
}
