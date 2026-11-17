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
            await output.WriteLineAsync(ConsoleMessages.Help);
            await PrintBalanceAsync(default(Money));

            while (!cancellationToken.IsCancellationRequested)
            {
                await output.WriteAsync(ConsoleMessages.Prompt);
                string? line = await input.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (!CommandParser.TryParse(line, out CommandKind command, out decimal amount))
                {
                    await output.WriteLineAsync(ConsoleMessages.InvalidCommand);
                    OperationResult balance = await service.GetBalanceAsync(id, cancellationToken);
                    await PrintBalanceAsync(balance.Balance);
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

                string message = result.Error switch
                {
                    WalletError.None => result.Payout is { } payout ? string.Format(CultureInfo.InvariantCulture, ConsoleMessages.PayoutFormat, Format(payout)) : ConsoleMessages.Success,
                    WalletError.InvalidAmount => ConsoleMessages.InvalidAmount,
                    WalletError.InsufficientFunds => ConsoleMessages.InsufficientFunds,
                    WalletError.InvalidBet => ConsoleMessages.InvalidBet,
                    WalletError.BalanceOverflow => ConsoleMessages.BalanceOverflow,
                    WalletError.ConcurrencyConflict => ConsoleMessages.ConcurrencyConflict,
                    _ => ConsoleMessages.WalletNotFound,
                };
                await output.WriteLineAsync(message);
                await PrintBalanceAsync(result.Balance);
            }

            await output.WriteLineAsync(ConsoleMessages.Goodbye);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync(ConsoleMessages.Cancelled);
            return 0;
        }
        catch (Exception)
        {
            await output.WriteLineAsync(ConsoleMessages.UnexpectedError);
            return 1;
        }
    }

    private Task PrintBalanceAsync(Money? balance) => output.WriteLineAsync(balance is { } money
        ? string.Format(CultureInfo.InvariantCulture, ConsoleMessages.BalanceFormat, Format(money)) : ConsoleMessages.BalanceUnavailable);

    private static string Format(Money money) => money.Amount.ToString(ConsoleMessages.MoneyFormat, CultureInfo.InvariantCulture);
}
