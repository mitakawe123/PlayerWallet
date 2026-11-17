namespace PlayerWallet.Console.Tests;

internal sealed class BlockingCommandReader : TextReader
{
    private readonly TaskCompletionSource _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Signals that the console began reading a command.</summary>
    public Task ReadStarted => _readStarted.Task;

    /// <summary>Waits for cancellation instead of returning a command.</summary>
    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        _readStarted.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return null;
    }
}
