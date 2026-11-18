using PlayerWallet.Domain;

namespace PlayerWallet.Application;

/// <summary>Stores independent wallet copies with optimistic concurrency.</summary>
public interface IWalletRepository
{

    /// <summary>Adds a new wallet, returning false when its identifier already exists.</summary>
    Task<bool> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default);

    /// <summary>Loads an independent wallet and its version, or null if absent.</summary>
    Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Atomically saves only when the stored version matches the supplied wallet version.</summary>
    Task<bool> SaveAsync(Wallet wallet, CancellationToken cancellationToken = default);
}
