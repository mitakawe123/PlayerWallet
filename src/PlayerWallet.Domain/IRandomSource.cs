namespace PlayerWallet.Domain;

/// <summary>Supplies random integers for selecting game outcomes.</summary>
public interface IRandomSource
{
    /// <summary>Returns an integer in the inclusive lower and exclusive upper bounds.</summary>
    int NextInt(int minimumInclusive, int maximumExclusive);
}
