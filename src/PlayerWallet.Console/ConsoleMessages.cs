using System.Text;

namespace PlayerWallet.Console;

/// <summary>Centralizes console text and display formats.</summary>
internal static class ConsoleMessages
{
    internal const string Help = "Commands: deposit <amount>, withdraw <amount>, bet <amount>, exit";

    internal const string Prompt = "> ";

    internal const string InvalidCommand = "Invalid command. Use deposit, withdraw, or bet with an amount, or exit.";

    internal const string Success = "Operation successful.";

    internal const string InvalidAmount = "Amount must be positive and have no fractional cents.";

    internal const string InsufficientFunds = "Insufficient funds.";

    internal const string InvalidBet = "Bet is outside the allowed limits.";

    internal const string BalanceOverflow = "The resulting balance is too large.";

    internal const string ConcurrencyConflict = "Wallet changed. This operation was not saved; no automatic retry was made.";

    internal const string WalletNotFound = "Wallet was not found.";

    internal const string Goodbye = "Goodbye!";

    internal const string Cancelled = "Session cancelled.";

    internal const string UnexpectedError = "An unexpected error ended the session. The operation outcome could not be confirmed.";

    internal const string BalanceUnavailable = "Balance unavailable.";

    internal static readonly CompositeFormat BalanceFormat = CompositeFormat.Parse("Balance: {0}");

    internal static readonly CompositeFormat PayoutFormat = CompositeFormat.Parse("Payout: {0}.");

    internal const string MoneyFormat = "$0.00";
}
