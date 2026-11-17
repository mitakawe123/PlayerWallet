using PlayerWallet.Domain;

namespace PlayerWallet.Application;

/// <summary>Describes an operation and the balance observed for its result.</summary>
/// <param name="Error">The failure code, or None for success.</param>
/// <param name="Balance">The observed balance, or null when no wallet exists.</param>
/// <param name="Payout">The committed gross payout, if this was a successful bet.</param>
public sealed record OperationResult(WalletError Error, Money? Balance, Money? Payout = null);
