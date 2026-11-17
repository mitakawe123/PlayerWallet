namespace PlayerWallet.Domain.Tests;

internal sealed class BoundaryRandom(int category, bool upper) : IRandomSource
{
    /// <summary>Gets the number of random draws consumed.</summary>
    public int Calls { get; private set; }

    /// <summary>Returns a controlled random value for this test.</summary>
    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        Calls++;
        if (Calls is 1)
        {
            Assert.Equal(0, minimumInclusive);
            Assert.Equal(100, maximumExclusive);
            return category;
        }

        Assert.Equal(category < 90 ? 1 : 1001, minimumInclusive);
        Assert.Equal(category < 90 ? 1001 : 5001, maximumExclusive);
        return upper ? maximumExclusive - 1 : minimumInclusive;
    }
}
