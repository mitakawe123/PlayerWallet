using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;
using PlayerWallet.Console.Infrastructure;

namespace PlayerWallet.Infrastructure.Tests;

/// <summary>Verifies snapshot isolation and conditional storage updates.</summary>
public sealed class InMemoryWalletRepositoryTests
{
    /// <summary>Keeps balances independent for wallets created through the same service.</summary>
    [Fact]
    public async Task DepositOnlyChangesTheTargetWallet()
    {
        var repository = new InMemoryWalletRepository();
        var service = new WalletService(repository);
        Guid first = await service.CreateWalletAsync();
        Guid second = await service.CreateWalletAsync();

        Assert.NotEqual(first, second);
        Assert.Null((await service.DepositAsync(first, 25m)).Error);
        Assert.Equal(25m, (await service.GetBalanceAsync(first)).Amount);
        Assert.Equal(0m, (await service.GetBalanceAsync(second)).Amount);
    }

    /// <summary>Requires explicit saves and does not retain caller-owned wallet references.</summary>
    [Fact]
    public async Task StoredStateIsIsolated()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        var ten = new Money(10);
        Assert.True(await repository.CreateAsync(wallet));
        Assert.False(await repository.CreateAsync(wallet));
        wallet.Deposit(ten);
        Wallet? first = await repository.GetAsync(wallet.Id);
        Assert.NotNull(first);
        Assert.Equal(0m, first.Balance.Amount);
        first.Deposit(ten);
        Wallet? beforeSave = await repository.GetAsync(wallet.Id);
        Assert.NotNull(beforeSave);
        Assert.Equal(0m, beforeSave.Balance.Amount);
        Assert.True(await repository.SaveAsync(first));
        first.Deposit(ten);
        Wallet? saved = await repository.GetAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(10m, saved.Balance.Amount);
        Assert.Equal(1, saved.Version);
    }

    /// <summary>Allows only one writer to save against the same version.</summary>
    [Fact]
    public async Task ConcurrentWritersCannotOverwriteEachOther()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        await repository.CreateAsync(wallet);
        var one = new Money(1);
        IEnumerable<Task<bool>> writers = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            var candidate = new Wallet(wallet.Id, one);
            return await repository.SaveAsync(candidate);
        }));
        bool[] results = await Task.WhenAll(writers);
        Assert.Single(results, success => success);
        Wallet? saved = await repository.GetAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.Version);
        Assert.Equal(1m, saved.Balance.Amount);
    }

    /// <summary>Handles missing wallets and cancelled operations without mutation.</summary>
    [Fact]
    public async Task MissingAndCancelledOperationsAreSafe()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        Assert.Null(await repository.GetAsync(wallet.Id));
        Assert.False(await repository.SaveAsync(wallet));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync(wallet, cancellation.Token));
        Assert.Null(await repository.GetAsync(wallet.Id));
        await repository.CreateAsync(wallet);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(wallet.Id, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(wallet, cancellation.Token));
        Wallet? stored = await repository.GetAsync(wallet.Id);
        Assert.NotNull(stored);
        Assert.Equal(0, stored.Version);
    }
}
