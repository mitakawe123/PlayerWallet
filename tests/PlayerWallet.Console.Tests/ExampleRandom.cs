using PlayerWallet.Domain;

namespace PlayerWallet.Console.Tests;

internal sealed class ExampleRandom : IRandomSource
{
    private int _calls;

    /// <summary>Returns a controlled random value for this test.</summary>
    public int NextInt(int minimumInclusive, int maximumExclusive) => ++_calls is 1 ? 90 : 3535;
}
