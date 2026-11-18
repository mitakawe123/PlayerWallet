using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Infrastructure;

/// <summary>Stores wallet snapshots behind one lock.</summary>
/// <remarks>
/// The lock protects the balance read, mutation, and write as one operation.
/// </remarks>
public sealed class InMemoryWalletRepository : IWalletRepository
{
    private const string MissingWalletMessage = "The wallet does not exist.";

    private readonly Dictionary<Guid, Wallet> _wallets = [];
    private readonly Lock _gate = new();

    /// <summary>Stores a new wallet snapshot if its identifier is unused.</summary>
    /// <param name="wallet">The wallet to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when the wallet was stored.</returns>
    public Task<bool> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_wallets.TryAdd(wallet.Id, new Wallet(wallet.Id, wallet.Balance)));
        }
    }

    /// <summary>Returns an independent wallet instance, or null when the wallet is absent.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A detached copy of the stored wallet.</returns>
    public Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Wallet? result = _wallets.TryGetValue(id, out Wallet? saved)
                ? new Wallet(saved.Id, saved.Balance)
                : null;
            return Task.FromResult(result);
        }
    }

    /// <summary>Applies an operation inside one critical section.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="apply">Computes the result from a detached copy of the wallet.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The result of the wallet operation.</returns>
    public Task<OperationResult> ExecuteAsync(
        Guid id,
        Func<Wallet, OperationResult> apply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_wallets.TryGetValue(id, out Wallet? saved))
            {
                throw new InvalidOperationException(MissingWalletMessage);
            }

            var candidate = new Wallet(saved.Id, saved.Balance);
            OperationResult result = apply(candidate);
            if (result.Error is not null)
            {
                return Task.FromResult(result);
            }

            _wallets[id] = candidate;
            return Task.FromResult(result);
        }
    }
}
