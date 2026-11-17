using System.Globalization;

namespace PlayerWallet.Console;

/// <summary>Parses the supported console command grammar without allocating token strings.</summary>
public static class CommandParser
{
    /// <summary>Parses a command with invariant decimal-point amounts and exact argument counts.</summary>
    public static bool TryParse(ReadOnlySpan<char> input, out CommandKind command, out decimal amount)
    {
        input = input.Trim();
        int separator = 0;
        while (separator < input.Length && !char.IsWhiteSpace(input[separator]))
        {
            separator++;
        }

        command = ParseCommand(input[..separator]);
        amount = 0;
        ReadOnlySpan<char> argument = input[separator..].Trim();
        if (command is CommandKind.Exit)
        {
            return argument.IsEmpty;
        }

        if (command is CommandKind.Unknown || argument.IsEmpty)
        {
            return false;
        }

        // Reject fractional cents before decimal parsing can round very long inputs.
        int point = argument.IndexOf('.');
        if (point >= 0 && argument[(point + 1)..].TrimEnd('0').Length > 2)
        {
            return false;
        }

        return decimal.TryParse(argument, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out amount);
    }

    private static CommandKind ParseCommand(ReadOnlySpan<char> token)
    {
        if (token.Equals(nameof(CommandKind.Deposit), StringComparison.OrdinalIgnoreCase))
        {
            return CommandKind.Deposit;
        }

        if (token.Equals(nameof(CommandKind.Withdraw), StringComparison.OrdinalIgnoreCase))
        {
            return CommandKind.Withdraw;
        }

        if (token.Equals(nameof(CommandKind.Bet), StringComparison.OrdinalIgnoreCase))
        {
            return CommandKind.Bet;
        }

        return token.Equals(nameof(CommandKind.Exit), StringComparison.OrdinalIgnoreCase)
            ? CommandKind.Exit
            : CommandKind.Unknown;
    }
}
