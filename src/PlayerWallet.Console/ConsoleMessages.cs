using System.Text;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console;

/// <summary>Centralizes console text and display formats.</summary>
internal static class ConsoleMessages
{
    internal const string Prompt = "Please, submit action:";

    internal const string InvalidCommand = "Invalid command. Use deposit, withdraw, or bet with an amount, or exit.";

    internal const string InvalidAmount = "Amount must be positive and have no fractional cents.";

    internal const string InsufficientFunds = "Insufficient funds.";

    internal const string InvalidBet = "Bet is outside the allowed limits.";

    internal const string Goodbye = "Thank you for playing! Hope to see you again soon.";

    internal const string ExitPrompt = "Press any key to exit.";

    internal const string Cancelled = "Session cancelled.";

    internal const string UnexpectedError = "An unexpected error ended the session. The operation outcome could not be confirmed.";

    internal const string WholeMoneyFormat = "$0";

    internal const string MoneyFormat = "$0.00";

    /// <summary>Returns the message shown for a wallet rule.</summary>
    /// <param name="error">The violated rule.</param>
    /// <returns>The message for the rule.</returns>
    internal static string For(WalletError error) => error switch
    {
        WalletError.InvalidAmount => InvalidAmount,
        WalletError.InsufficientFunds => InsufficientFunds,
        WalletError.InvalidBet => InvalidBet,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown wallet rule."),
    };

    internal static readonly CompositeFormat DepositFormat =
        CompositeFormat.Parse("Your deposit of {0} was successful. Your current balance is: {1}");

    internal static readonly CompositeFormat WithdrawalFormat =
        CompositeFormat.Parse("Your withdrawal of {0} was successful. Your current balance is: {1}");

    internal static readonly CompositeFormat WinFormat =
        CompositeFormat.Parse("Congrats - you won {0}! Your current balance is: {1}");

    internal static readonly CompositeFormat LossFormat =
        CompositeFormat.Parse("No luck this time! Your current balance is: {0}");

    internal static readonly CompositeFormat FailureFormat =
        CompositeFormat.Parse("{0} Your current balance is: {1}");
}
