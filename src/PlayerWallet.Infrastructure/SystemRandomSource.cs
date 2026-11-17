using PlayerWallet.Domain;

namespace PlayerWallet.Infrastructure;

/// <summary>Uses the runtime random generator for this simulated game.</summary>
public sealed class SystemRandomSource : IRandomSource
{
    /// <summary>Returns an integer in the inclusive lower and exclusive upper bounds.</summary>
    public int NextInt(int minimumInclusive, int maximumExclusive) =>
        Random.Shared.Next(minimumInclusive, maximumExclusive);
}
