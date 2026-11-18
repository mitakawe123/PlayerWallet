using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Application.Tests;

internal sealed class TestRepository : IWalletRepository
{
    private Guid _id;
    private Money _balance;
    private long _version;

    /// <summary>Gets or sets whether saves simulate a version conflict.</summary>
    public bool Conflict { get; set; }

    /// <summary>Gets or sets whether saves throw a storage error.</summary>
    public bool Fail { get; set; }

    /// <summary>Gets the number of attempted saves.</summary>
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
        Task.FromResult<Wallet?>(id == this._id ? new Wallet(id, _balance, _version) : null);

    /// <summary>Simulates a conditional save or configured failure.</summary>
    public Task<bool> SaveAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        Saves++;
        if (Fail)
        {
            throw new IOException("Storage unavailable.");
        }

        if (Conflict || wallet.Version != _version)
        {
            return Task.FromResult(false);
        }

        _balance = wallet.Balance;
        _version++;
        return Task.FromResult(true);
    }
}
