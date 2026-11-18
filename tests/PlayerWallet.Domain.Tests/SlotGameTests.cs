namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies exact probability boundaries and payout endpoints.</summary>
public sealed class SlotGameTests
{
    /// <summary>Provides category boundaries and the expected payouts for a $5 stake.</summary>
    public static TheoryData<int, bool, decimal> Outcomes => new()
    {
        { 0, false, 0m },
        { 49, false, 0m },
        { 50, false, 0.01m },
        { 89, true, 10m },
        { 90, false, 10.01m },
        { 99, true, 50m },
    };

    /// <summary>Checks both edges of every outcome category and payout range.</summary>
    [Theory]
    [MemberData(nameof(Outcomes))]
    public void OutcomeBoundariesAreExact(int category, bool upper, decimal expected)
    {
        var random = new BoundaryRandom(category, upper);
        var game = new SlotGame(new GameRules(), random);
        var stake = new Money(5m);
        Assert.Equal(expected, game.Play(stake).Amount);
        Assert.Equal(category < 50 ? 1 : 2, random.Calls);
    }

    /// <summary>Rejects invalid probabilities and stake limits at construction.</summary>
    [Fact]
    public void RulesRejectInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(minimumBet: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(minimumBet: 0.001m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(maximumBet: 0.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(lossPercent: 70));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameRules(smallWinPercent: -1));
    }
}
