using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Stores wallet balances as one unit of work.</summary>
/// <remarks>
/// The contract is deliberately a single atomic call rather than load/save pairs, so an
/// implementation backed by a database can map <see cref="ExecuteAsync"/> onto one
/// transaction and keep the same semantics as the in-memory store.
/// </remarks>
public interface IWalletRepository
{
    /// <summary>Adds a new wallet, returning false when its identifier already exists.</summary>
    /// <param name="wallet">The wallet to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when the wallet was stored.</returns>
    Task<bool> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default);

    /// <summary>Loads an independent wallet snapshot, or null when the wallet is absent.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A detached copy of the stored wallet.</returns>
    Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Applies one operation to a wallet atomically.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="apply">
    /// Computes the result from a detached copy of the wallet. It runs inside the atomic
    /// section, so it must be synchronous, must not perform I/O, and must not call back
    /// into this store. It must leave the wallet unchanged when it returns a rejection.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The result of the wallet operation.</returns>
    /// <exception cref="InvalidOperationException">The wallet does not exist.</exception>
    Task<OperationResult> ExecuteAsync(
        Guid id,
        Func<Wallet, OperationResult> apply,
        CancellationToken cancellationToken = default);
}
