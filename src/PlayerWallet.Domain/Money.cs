namespace PlayerWallet.Domain;

/// <summary>Represents a nonnegative amount with cent precision.</summary>
public readonly record struct Money
{
    private const string InvalidAmountMessage = "Money must be nonnegative, within the supported range, and have no fractional cents.";

    /// <summary>Gets the largest amount that preserves exact cent arithmetic.</summary>
    public const decimal MaximumAmount = decimal.MaxValue / 100m;

    /// <summary>Creates a valid amount, rejecting negative values, fractional cents, and unsupported values.</summary>
    public Money(decimal amount)
    {
        if (!IsValid(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, InvalidAmountMessage);
        }

        Amount = amount;
    }

    /// <summary>Gets the amount in dollars.</summary>
    public decimal Amount { get; }

    /// <summary>Checks whether an amount can be represented without throwing for invalid input.</summary>
    public static bool IsValid(decimal amount) =>
        amount is >= 0 and <= MaximumAmount && decimal.Round(amount, decimals: 2) == amount;
}
