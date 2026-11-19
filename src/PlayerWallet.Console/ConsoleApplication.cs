using System.Globalization;
using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console;

/// <summary>Runs a single wallet session over injectable text streams.</summary>
public sealed class ConsoleApplication(WalletService service, TextReader input, TextWriter output)
{
    /// <summary>Runs commands until exit, end-of-input, or an unexpected failure.</summary>
    public async Task<int> RunAsync()
    {
        try
        {
            Guid id = await service.CreateWalletAsync();

            while (true)
            {
                await output.WriteLineAsync(ConsoleMessages.Prompt);
                string? line = await input.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (!CommandParser.TryParse(line, out CommandKind command, out decimal amount))
                {
                    Money balance = await service.GetBalanceAsync(id);
                    await WriteFailureAsync(ConsoleMessages.InvalidCommand, balance);
                    continue;
                }

                if (command is CommandKind.Exit)
                {
                    break;
                }

                OperationResult result = command switch
                {
                    CommandKind.Deposit => await service.DepositAsync(id, amount),
                    CommandKind.Withdraw => await service.WithdrawAsync(id, amount),
                    CommandKind.Bet => await service.PlaceBetAsync(id, amount),
                    _ => throw new InvalidOperationException(ConsoleMessages.InvalidCommand),
                };

                await WriteResultAsync(command, amount, result);
            }

            await output.WriteLineAsync(ConsoleMessages.Goodbye);
            await output.WriteLineAsync();
            await output.WriteLineAsync(ConsoleMessages.ExitPrompt);
            return 0;
        }
        catch (Exception)
        {
            await output.WriteLineAsync(ConsoleMessages.UnexpectedError);
            return 1;
        }
    }

    /// <summary>Formats a successful deposit, withdrawal, win, or loss.</summary>
    /// <param name="command">The command that produced the result.</param>
    /// <param name="amount">The submitted amount.</param>
    /// <param name="result">The successful operation result.</param>
    /// <returns>The message shown for the operation.</returns>
    internal static string FormatSuccess(CommandKind command, decimal amount, OperationResult result)
    {
        string balance = FormatAmount(result.Balance.Amount);
        return command switch
        {
            CommandKind.Deposit => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.DepositFormat,
                FormatSubmittedAmount(amount),
                balance),
            CommandKind.Withdraw => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.WithdrawalFormat,
                FormatSubmittedAmount(amount),
                balance),
            CommandKind.Bet when result.Payout.Amount > 0 => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.WinFormat,
                FormatAmount(result.Payout.Amount),
                balance),
            CommandKind.Bet => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.LossFormat,
                balance),
            _ => throw new InvalidOperationException(ConsoleMessages.InvalidCommand),
        };
    }

    private async Task WriteResultAsync(CommandKind command, decimal amount, OperationResult result)
    {
        if (result.Error is { } error)
        {
            await WriteFailureAsync(ConsoleMessages.For(error), result.Balance);
            return;
        }

        await output.WriteLineAsync(FormatSuccess(command, amount, result));
    }

    private Task WriteFailureAsync(string message, Money balance) =>
        output.WriteLineAsync(
            string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.FailureFormat,
                message,
                FormatAmount(balance.Amount)));

    private static string FormatAmount(decimal amount) => amount == decimal.Truncate(amount)
        ? amount.ToString(ConsoleMessages.WholeMoneyFormat, CultureInfo.InvariantCulture)
        : amount.ToString(ConsoleMessages.MoneyFormat, CultureInfo.InvariantCulture);

    private static string FormatSubmittedAmount(decimal amount) => amount.ToString(
        amount.Scale == 0 ? ConsoleMessages.WholeMoneyFormat : ConsoleMessages.MoneyFormat,
        CultureInfo.InvariantCulture);
}
