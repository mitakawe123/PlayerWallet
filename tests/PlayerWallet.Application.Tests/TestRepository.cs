using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Application.Tests;

internal sealed class TestRepository : IWalletRepository
{
    private readonly Dictionary<Guid, OperationResult> _applied = [];
    private Guid _id;
    private Money _balance;

    /// <summary>Gets or sets whether execution fails before recording anything.</summary>
    public bool Fail { get; set; }

    /// <summary>Gets the number of operations that were committed.</summary>
    public int Saves { get; private set; }

    /// <summary>Creates the isolated test wallet snapshot.</summary>
    public Task<bool> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        _id = wallet.Id;
        _balance = wallet.Balance;
        return Task.FromResult(true);
    }

    /// <summary>Loads a fresh wallet instance from the test snapshot.</summary>
    public Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Wallet?>(id == _id ? new Wallet(id, _balance) : null);

    /// <summary>Simulates one atomic commit, a replay, or a configured storage failure.</summary>
    public Task<OperationResult> ExecuteAsync(
        Guid id,
        Guid operationId,
        Func<Wallet, OperationResult> apply,
        CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new IOException("Storage unavailable.");
        }

        if (_applied.TryGetValue(operationId, out OperationResult replay))
        {
            return Task.FromResult(replay);
        }

        if (id != _id)
        {
            throw new InvalidOperationException("The wallet does not exist.");
        }

        var candidate = new Wallet(id, _balance);
        OperationResult result = apply(candidate);
        if (result.Error is not null)
        {
            return Task.FromResult(result);
        }

        _balance = candidate.Balance;
        _applied[operationId] = result;
        Saves++;
        return Task.FromResult(result);
    }
}
