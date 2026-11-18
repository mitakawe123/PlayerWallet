using System.Globalization;
using PlayerWallet.Application;
using PlayerWallet.Domain;
using PlayerWallet.Infrastructure;

namespace PlayerWallet.Console.Tests;

/// <summary>Verifies command parsing, result formatting, and complete console sessions.</summary>
public sealed class ConsoleApplicationTests
{
    /// <summary>Rejects malformed commands and ambiguous amount notation.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("deposit")]
    [InlineData("deposit 1 extra")]
    [InlineData("deposit 1,50")]
    [InlineData("deposit 1e2")]
    [InlineData("deposit 1.000000000000000000000000000000001")]
    [InlineData("withdraw NaN")]
    [InlineData("exit 1")]
    [InlineData("unknown 2")]
    [InlineData("1 10")]
    [InlineData("Deposit,Bet 5")]
    [InlineData("deposit 1 2")]
    public void InvalidCommandsAreRejected(string input) => Assert.False(CommandParser.TryParse(input, out _, out _));

    /// <summary>Supports case-insensitive commands and surrounding whitespace.</summary>
    [Fact]
    public void ParserAcceptsWhitespaceAndCase()
    {
        Assert.True(CommandParser.TryParse("  DePoSiT   10.50  ", out CommandKind command, out decimal amount));
        Assert.Equal(CommandKind.Deposit, command);
        Assert.Equal(10.50m, amount);
    }

    /// <summary>Parses span slices and whitespace without accepting additional arguments.</summary>
    [Fact]
    public void ParserReadsSpanSlices()
    {
        const string Input = "xx\tBeT\t5.00\tyy";
        Assert.True(CommandParser.TryParse(Input.AsSpan(2, Input.Length - 4), out CommandKind command, out decimal amount));
        Assert.Equal(CommandKind.Bet, command);
        Assert.Equal(5m, amount);
        Assert.True(CommandParser.TryParse(" ExIt ".AsSpan(), out command, out _));
        Assert.Equal(CommandKind.Exit, command);
    }

    /// <summary>Formats deposit, withdrawal, win, and loss messages as the brief shows.</summary>
    [Fact]
    public void ResultFormattingMatchesTheBrief()
    {
        Assert.Equal(
            "Your deposit of $10 was successful. Your current balance is: $10",
            ConsoleApplication.FormatSuccess(
                CommandKind.Deposit,
                10m,
                new OperationResult(WalletError.None, new Money(10m))));
        Assert.Equal(
            "Your withdrawal of $40.35 was successful. Your current balance is: $0",
            ConsoleApplication.FormatSuccess(
                CommandKind.Withdraw,
                40.35m,
                new OperationResult(WalletError.None, new Money(0m))));
        Assert.Equal(
            "Congrats - you won $35.35! Your current balance is: $40.35",
            ConsoleApplication.FormatSuccess(
                CommandKind.Bet,
                5m,
                new OperationResult(WalletError.None, new Money(40.35m), new Money(35.35m))));
        Assert.Equal(
            "No luck this time! Your current balance is: $5",
            ConsoleApplication.FormatSuccess(
                CommandKind.Bet,
                5m,
                new OperationResult(WalletError.None, new Money(5m))));
    }

    /// <summary>Runs a session, recovers from errors, and exits cleanly.</summary>
    [Fact]
    public async Task FullSessionShowsCommittedBalances()
    {
        using var input = new StringReader("deposit 10\nbet 5\nwithdraw 100\nwat\nwithdraw 5\nexit\ndeposit 5\n");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var service = new WalletService(new InMemoryWalletRepository());
        var app = new ConsoleApplication(service, input, output);
        Assert.Equal(0, await app.RunAsync());
        string transcript = output.ToString();
        Assert.Contains("Your deposit of $10 was successful. Your current balance is: $10", transcript, StringComparison.Ordinal);
        Assert.True(
            transcript.Contains("Congrats - you won $", StringComparison.Ordinal)
            ^ transcript.Contains("No luck this time!", StringComparison.Ordinal));
        Assert.Contains("Insufficient funds. Your current balance is: $", transcript, StringComparison.Ordinal);
        Assert.Contains("Invalid command.", transcript, StringComparison.Ordinal);
        Assert.Contains("Your withdrawal of $5 was successful. Your current balance is: $", transcript, StringComparison.Ordinal);
        Assert.Contains("Thank you for playing!", transcript, StringComparison.Ordinal);
        Assert.EndsWith("Press any key to exit." + Environment.NewLine, transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Your deposit of $5", transcript, StringComparison.Ordinal);
    }

    /// <summary>Exits cleanly when cancelled while waiting for a command.</summary>
    [Fact]
    public async Task CancellationInterruptsPendingRead()
    {
        using var cancellation = new CancellationTokenSource();
        using var input = new BlockingCommandReader();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var service = new WalletService(new InMemoryWalletRepository());
        var app = new ConsoleApplication(service, input, output);
        var timeout = TimeSpan.FromSeconds(5);

        Task<int> session = app.RunAsync(cancellation.Token);
        await input.ReadStarted.WaitAsync(timeout);
        await cancellation.CancelAsync();

        Assert.Equal(0, await session.WaitAsync(timeout));
        string transcript = output.ToString();
        Assert.EndsWith("Session cancelled." + Environment.NewLine, transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Operation successful.", transcript, StringComparison.Ordinal);
    }

    /// <summary>Ends gracefully on EOF, including after rejected amounts.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("deposit -1\ndeposit 0.001\n")]
    public async Task EndOfInputExitsCleanly(string commands)
    {
        using var input = new StringReader(commands);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var service = new WalletService(new InMemoryWalletRepository());
        Assert.Equal(0, await new ConsoleApplication(service, input, output).RunAsync());
        Assert.Contains("Thank you for playing!", output.ToString(), StringComparison.Ordinal);
    }
}
