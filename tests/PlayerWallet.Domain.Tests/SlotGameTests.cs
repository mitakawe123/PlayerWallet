namespace PlayerWallet.Domain.Tests;

/// <summary>Verifies exact probability boundaries and payout endpoints.</summary>
public sealed class SlotGameTests
{
    /// <summary>Checks both edges of every outcome category and payout range.</summary>
    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(49, false, 0)]
    [InlineData(50, false, 0.01)]
    [InlineData(89, true, 10)]
    [InlineData(90, false, 10.01)]
    [InlineData(99, true, 50)]
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
