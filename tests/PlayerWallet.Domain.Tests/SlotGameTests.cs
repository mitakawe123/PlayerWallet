namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies the fixed game rules and the payout band for each outcome category.</summary>
public sealed class SlotGameTests
{
    private static readonly Money Stake = new(5m);

    /// <summary>Pays nothing for every losing category.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(49)]
    public void LosingCategoriesPayNothing(int category) =>
        Assert.Equal(0m, SlotGame.PayoutFor(category, Stake).Amount);

    /// <summary>Pays from one cent through twice the stake for small-win categories.</summary>
    [Theory]
    [InlineData(50)]
    [InlineData(70)]
    [InlineData(89)]
    public void SmallWinsPayFromOneCentThroughTwiceTheStake(int category) =>
        Assert.InRange(SlotGame.PayoutFor(category, Stake).Amount, 0.01m, 2 * Stake.Amount);

    /// <summary>Pays above twice the stake through ten times the stake for large-win categories.</summary>
    [Theory]
    [InlineData(90)]
    [InlineData(95)]
    [InlineData(99)]
    public void LargeWinsPayAboveTwiceThroughTenTimesTheStake(int category) =>
        Assert.InRange(SlotGame.PayoutFor(category, Stake).Amount, (2 * Stake.Amount) + 0.01m, 10 * Stake.Amount);

    /// <summary>Keeps repeated draws inside the band selected by the category.</summary>
    [Theory]
    [InlineData(50)]
    [InlineData(89)]
    [InlineData(90)]
    [InlineData(99)]
    public void RepeatedDrawsStayInsideTheSelectedBand(int category)
    {
        decimal minimum = category < 90 ? 0.01m : (2 * Stake.Amount) + 0.01m;
        decimal maximum = category < 90 ? 2 * Stake.Amount : 10 * Stake.Amount;
        for (int draw = 0; draw < 500; draw++)
        {
            Assert.InRange(SlotGame.PayoutFor(category, Stake).Amount, minimum, maximum);
        }
    }

    /// <summary>Rejects categories outside the 0 through 99 range.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void UnknownCategoriesAreRejected(int category) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.PayoutFor(category, Stake));

    /// <summary>Rejects stakes outside the inclusive game limits.</summary>
    [Theory]
    [InlineData(0.99)]
    [InlineData(10.01)]
    public void PlayRejectsStakesOutsideTheLimits(decimal stake) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGame.Play(new Money(stake)));

    /// <summary>Accepts the inclusive stake limits and stays within the maximum payout.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void PlayAcceptsInclusiveStakeLimits(decimal stake) =>
        Assert.InRange(SlotGame.Play(new Money(stake)).Amount, 0m, 10 * stake);
}
