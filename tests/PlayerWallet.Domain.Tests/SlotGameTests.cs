using PlayerWallet.Console.Domain;

namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies the fixed game rules, category boundaries, and payout bands.</summary>
public sealed class SlotGameTests
{
    private static readonly Money Stake = new(5m);

    /// <summary>Provides stakes outside the inclusive game limits.</summary>
    public static TheoryData<decimal> InvalidStakes => new() { 0.99m, 10.01m };

    /// <summary>Provides stakes and whether they are inside the inclusive game limits.</summary>
    public static TheoryData<decimal, bool> StakeLimits => new()
    {
        { 0m, false },
        { 0.99m, false },
        { 1m, true },
        { 10m, true },
        { 10.01m, false },
    };

    /// <summary>Accepts exactly the inclusive game limits.</summary>
    [Theory]
    [MemberData(nameof(StakeLimits))]
    public void StakeLimitsAreInclusive(decimal stake, bool expected) =>
        Assert.Equal(expected, SlotGame.IsStakeWithinLimits(stake));

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

    /// <summary>Draws the band from 0 through 99 and prices a loss without a second draw.</summary>
    [Fact]
    public void ScriptedLossDrawsOnlyTheBand()
    {
        var random = new ScriptedRandom(0);
        Assert.Equal(0m, SlotGame.Play(Stake, random).Amount);
        Assert.Collection(random.Ranges, range => Assert.Equal((0, 100), range));
    }

    /// <summary>Prices a small win from one cent through twice the stake.</summary>
    [Fact]
    public void ScriptedSmallWinDrawsWithinTwiceTheStake()
    {
        var random = new ScriptedRandom(50, 1);
        Assert.Equal(0.01m, SlotGame.Play(Stake, random).Amount);
        Assert.Collection(
            random.Ranges,
            range => Assert.Equal((0, 100), range),
            range => Assert.Equal((1, 1001), range));
    }

    /// <summary>Spreads small wins across the whole band instead of always paying twice the stake.</summary>
    [Fact]
    public void SmallWinsCoverTheWholeBand()
    {
        const int Rounds = 10_000;
        var random = new Random(20260101);
        decimal total = 0m;
        decimal minimum = decimal.MaxValue;
        decimal maximum = 0m;
        for (int round = 0; round < Rounds; round++)
        {
            decimal payout = SlotGame.PayoutFor(PayoutBand.SmallWin, Stake, random).Amount;
            total += payout;
            minimum = Math.Min(minimum, payout);
            maximum = Math.Max(maximum, payout);
        }

        decimal mean = total / Rounds;
        Assert.InRange(mean, 0.9m * Stake.Amount, 1.1m * Stake.Amount);
        Assert.True(minimum < 0.1m * Stake.Amount, $"The smallest small win was {minimum}.");
        Assert.True(maximum > 1.9m * Stake.Amount, $"The largest small win was {maximum}.");
    }

    /// <summary>Prices a large win above twice the stake through ten times the stake.</summary>
    [Fact]
    public void ScriptedLargeWinDrawsAboveTwiceTheStake()
    {
        var random = new ScriptedRandom(90, 1001);
        Assert.Equal(10.01m, SlotGame.Play(Stake, random).Amount);
        Assert.Collection(
            random.Ranges,
            range => Assert.Equal((0, 100), range),
            range => Assert.Equal((1001, 5001), range));
    }

    /// <summary>Returns about the stake over many rounds, as the published bands imply.</summary>
    [Fact]
    public void RoundsReturnAboutTheStakeOnAverage()
    {
        const int Rounds = 200_000;
        var random = new Random(20260101);
        decimal paid = 0m;
        for (int round = 0; round < Rounds; round++)
        {
            paid += SlotGame.Play(Stake, random).Amount;
        }

        Assert.InRange(paid / (Rounds * Stake.Amount), 0.98m, 1.02m);
    }

    /// <summary>Returns queued draws and records the inclusive ranges it was asked for.</summary>
    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _values;
        private readonly List<(int Min, int Max)> _ranges = [];

        public ScriptedRandom(params int[] values) => _values = new(values);

        public IReadOnlyList<(int Min, int Max)> Ranges => _ranges;

        public override int Next(int minValue, int maxValue)
        {
            _ranges.Add((minValue, maxValue));
            int value = _values.Dequeue();
            Assert.InRange(value, minValue, maxValue - 1);
            return value;
        }
    }
}
