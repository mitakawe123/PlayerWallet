using PlayerWallet.Domain;

namespace PlayerWallet.Console.Tests;

internal sealed class LosingRandom : IRandomSource
{
    /// <summary>Returns a losing outcome category.</summary>
    public int NextInt(int minimumInclusive, int maximumExclusive) => minimumInclusive;
}
