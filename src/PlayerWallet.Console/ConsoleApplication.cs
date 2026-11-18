using System.Globalization;
using PlayerWallet.Application;
using PlayerWallet.Domain;

namespace PlayerWallet.Console;

/// <summary>Runs a single wallet session over injectable text streams.</summary>
public sealed class ConsoleApplication(WalletService service, TextReader input, TextWriter output)
{
    /// <summary>Runs commands until exit, end-of-input, cancellation, or an unexpected failure.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Guid id = await service.CreateWalletAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                await output.WriteLineAsync(ConsoleMessages.Prompt.AsMemory(), cancellationToken);
                string? line = await input.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (!CommandParser.TryParse(line, out CommandKind command, out decimal amount))
                {
                    OperationResult balance = await service.GetBalanceAsync(id, cancellationToken);
                    await WriteFailureAsync(ConsoleMessages.InvalidCommand, balance.Balance, cancellationToken);
                    continue;
                }

                if (command is CommandKind.Exit)
                {
                    break;
                }

                OperationResult result = command switch
                {
                    CommandKind.Deposit => await service.DepositAsync(id, amount, cancellationToken),
                    CommandKind.Withdraw => await service.WithdrawAsync(id, amount, cancellationToken),
                    CommandKind.Bet => await service.PlaceBetAsync(id, amount, cancellationToken),
                    _ => throw new InvalidOperationException(ConsoleMessages.InvalidCommand),
                };

                await WriteResultAsync(command, amount, result, cancellationToken);
            }

            await output.WriteLineAsync(ConsoleMessages.Goodbye.AsMemory(), cancellationToken);
            await output.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken);
            await output.WriteLineAsync(ConsoleMessages.ExitPrompt.AsMemory(), cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync(ConsoleMessages.Cancelled.AsMemory(), CancellationToken.None);
            return 0;
        }
        catch (Exception)
        {
            await output.WriteLineAsync(ConsoleMessages.UnexpectedError.AsMemory(), CancellationToken.None);
            return 1;
        }
    }

    private async Task WriteResultAsync(
        CommandKind command,
        decimal amount,
        OperationResult result,
        CancellationToken cancellationToken)
    {
        if (result.Error is WalletError.WalletNotFound)
        {
            await output.WriteLineAsync(ConsoleMessages.WalletNotFound.AsMemory(), cancellationToken);
            return;
        }

        if (result.Error is not WalletError.None)
        {
            await WriteFailureAsync(ErrorMessage(result.Error), result.Balance, cancellationToken);
            return;
        }

        await output.WriteLineAsync(FormatSuccess(command, amount, result).AsMemory(), cancellationToken);
    }

    /// <summary>Formats a successful deposit, withdrawal, win, or loss.</summary>
    /// <param name="command">The command that produced the result.</param>
    /// <param name="amount">The submitted amount.</param>
    /// <param name="result">The successful operation result.</param>
    /// <returns>The message shown for the operation.</returns>
    internal static string FormatSuccess(CommandKind command, decimal amount, OperationResult result)
    {
        string balance = FormatBalance(result.Balance);
        return command switch
        {
            CommandKind.Deposit => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.DepositFormat,
                FormatAmount(amount),
                balance),
            CommandKind.Withdraw => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.WithdrawalFormat,
                FormatAmount(amount),
                balance),
            CommandKind.Bet when result.Payout is { } payout => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.WinFormat,
                FormatAmount(payout.Amount),
                balance),
            CommandKind.Bet => string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.LossFormat,
                balance),
            _ => throw new InvalidOperationException(ConsoleMessages.InvalidCommand),
        };
    }

    private Task WriteFailureAsync(string message, Money? balance, CancellationToken cancellationToken) =>
        output.WriteLineAsync(
            string.Format(
                CultureInfo.InvariantCulture,
                ConsoleMessages.FailureFormat,
                message,
                FormatBalance(balance)).AsMemory(),
            cancellationToken);

    private static string ErrorMessage(WalletError error) => error switch
    {
        WalletError.InvalidAmount => ConsoleMessages.InvalidAmount,
        WalletError.InsufficientFunds => ConsoleMessages.InsufficientFunds,
        WalletError.InvalidBet => ConsoleMessages.InvalidBet,
        WalletError.BalanceOverflow => ConsoleMessages.BalanceOverflow,
        WalletError.ConcurrencyConflict => ConsoleMessages.ConcurrencyConflict,
        _ => ConsoleMessages.WalletNotFound,
    };

    private static string FormatBalance(Money? balance) => balance is { } money
        ? FormatAmount(money.Amount)
        : ConsoleMessages.BalanceUnavailable;

    private static string FormatAmount(decimal amount) => amount == decimal.Truncate(amount)
        ? amount.ToString(ConsoleMessages.WholeMoneyFormat, CultureInfo.InvariantCulture)
        : amount.ToString(ConsoleMessages.MoneyFormat, CultureInfo.InvariantCulture);
}
