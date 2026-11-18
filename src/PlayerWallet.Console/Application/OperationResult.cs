using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Describes a committed balance change or the rule that rejected it.</summary>
/// <param name="Balance">The balance after the operation, or the unchanged balance when rejected.</param>
/// <param name="Payout">The committed gross payout; zero for a loss and for non-bet operations.</param>
/// <param name="Error">The wallet rule that rejected the operation, or null when it succeeded.</param>
public readonly record struct OperationResult(Money Balance, Money Payout, WalletError? Error)
{
    /// <summary>Creates a result for a committed operation.</summary>
    /// <param name="balance">The balance after the operation.</param>
    /// <param name="payout">The gross payout, or zero when the operation paid nothing.</param>
    /// <returns>A successful result.</returns>
    public static OperationResult Success(Money balance, Money payout = default) => new(balance, payout, null);

    /// <summary>Creates a result for an operation a wallet rule rejected.</summary>
    /// <param name="error">The violated rule.</param>
    /// <param name="balance">The unchanged balance.</param>
    /// <returns>A rejected result.</returns>
    public static OperationResult Rejected(WalletError error, Money balance) => new(balance, default, error);
}
