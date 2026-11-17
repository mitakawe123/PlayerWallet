using PlayerWallet.Domain;

namespace PlayerWallet.Application.Tests;

internal sealed class TestRandom : IRandomSource
{
    /// <summary>Gets the number of random draws consumed.</summary>
    public int Calls { get; private set; }

    /// <summary>Returns a controlled random value for this test.</summary>
    public int NextInt(int minimumInclusive, int maximumExclusive) => ++Calls % 2 is 1 ? 90 : 3535;
}
