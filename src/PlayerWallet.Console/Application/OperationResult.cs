using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Describes an operation and the balance observed for its result.</summary>
/// <param name="Error">The failure code, or None for success.</param>
/// <param name="Balance">The observed balance, or null when no wallet exists.</param>
/// <param name="Payout">The committed gross payout, or null for a loss or a non-bet operation.</param>
public sealed record OperationResult(WalletError Error, Money? Balance, Money? Payout = null);
