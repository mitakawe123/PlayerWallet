using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Infrastructure;

/// <summary>Stores independent wallet copies behind atomic dictionary operations.</summary>
public sealed class InMemoryWalletRepository : IWalletRepository
{
    private readonly Dictionary<Guid, Wallet> _wallets = [];
    private readonly Lock _gate = new();

    /// <summary>Stores a new wallet snapshot if its identifier is unused.</summary>
    public Task<bool> CreateAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_wallets.TryAdd(wallet.Id, new Wallet(wallet.Id, wallet.Balance)));
        }
    }

    /// <summary>Returns an independent wallet instance and its current version.</summary>
    public Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Wallet? result = _wallets.TryGetValue(id, out Wallet? saved)
                ? new Wallet(saved.Id, saved.Balance, saved.Version)
                : null;
            return Task.FromResult(result);
        }
    }

    /// <summary>Replaces the stored snapshot only if its version matches.</summary>
    public Task<bool> SaveAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_wallets.TryGetValue(wallet.Id, out Wallet? saved) || saved.Version != wallet.Version)
            {
                return Task.FromResult(false);
            }

            _wallets[wallet.Id] = new Wallet(wallet.Id, wallet.Balance, checked(wallet.Version + 1));
            return Task.FromResult(true);
        }
    }
}
