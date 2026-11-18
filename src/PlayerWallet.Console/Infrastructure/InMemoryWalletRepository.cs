using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Infrastructure;

/// <summary>Stores wallet snapshots and applied operation results behind one lock.</summary>
/// <remarks>
/// The lock is the in-memory equivalent of a transaction: the replay check, the version-free
/// read, the caller's mutation, and the ledger write all happen inside one critical section.
/// A database implementation replaces it with a transaction and a unique index on the
/// operation identifier; nothing outside this class has to change.
/// </remarks>
public sealed class InMemoryWalletRepository : IWalletRepository
{
    private const string MissingWalletMessage = "The wallet does not exist.";

    private readonly Dictionary<Guid, Wallet> _wallets = [];
    private readonly Dictionary<Guid, OperationResult> _applied = [];
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

    /// <summary>Applies an operation and records its result inside one critical section.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="operationId">The identifier that makes this operation safe to replay.</param>
    /// <param name="apply">Computes the result from a detached copy of the wallet.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed result, or the result recorded for a replay.</returns>
    public Task<OperationResult> ExecuteAsync(
        Guid id,
        Guid operationId,
        Func<Wallet, OperationResult> apply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apply);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_applied.TryGetValue(operationId, out OperationResult replay))
            {
                return Task.FromResult(replay);
            }

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
            _applied[operationId] = result;
            return Task.FromResult(result);
        }
    }
}
