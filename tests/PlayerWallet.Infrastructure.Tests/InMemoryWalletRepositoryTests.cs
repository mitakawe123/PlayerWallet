using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;
using PlayerWallet.Console.Infrastructure;

namespace PlayerWallet.Infrastructure.Tests;

/// <summary>Verifies snapshot isolation and atomic execution.</summary>
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

    /// <summary>Stores snapshots rather than caller-owned wallet instances.</summary>
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
        Wallet? beforeCommit = await repository.GetAsync(wallet.Id);
        Assert.NotNull(beforeCommit);
        Assert.Equal(0m, beforeCommit.Balance.Amount);

        OperationResult committed = await repository.ExecuteAsync(
            wallet.Id,
            candidate =>
            {
                candidate.Deposit(ten);
                return OperationResult.Success(candidate.Balance);
            });
        Assert.Equal(10m, committed.Balance.Amount);

        first.Deposit(ten);
        Wallet? saved = await repository.GetAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(10m, saved.Balance.Amount);
    }

    /// <summary>Applies concurrent updates without losing any of them.</summary>
    [Fact]
    public async Task ConcurrentUpdatesAreAllApplied()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        await repository.CreateAsync(wallet);

        Task<OperationResult>[] updates =
        [
            .. Enumerable.Range(0, 20).Select(_ => Task.Run(() => repository.ExecuteAsync(
                wallet.Id,
                candidate =>
                {
                    candidate.Deposit(new Money(1));
                    return OperationResult.Success(candidate.Balance);
                }))),
        ];

        await Task.WhenAll(updates);
        Wallet? saved = await repository.GetAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(20m, saved.Balance.Amount);
    }

    /// <summary>Handles missing wallets and cancelled operations without mutation.</summary>
    [Fact]
    public async Task MissingAndCancelledOperationsAreSafe()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        Assert.Null(await repository.GetAsync(wallet.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteAsync(
            wallet.Id,
            candidate => OperationResult.Success(candidate.Balance)));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync(wallet, cancellation.Token));
        Assert.Null(await repository.GetAsync(wallet.Id));

        await repository.CreateAsync(wallet);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(wallet.Id, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ExecuteAsync(
            wallet.Id,
            candidate => OperationResult.Success(candidate.Balance),
            cancellation.Token));

        Wallet? stored = await repository.GetAsync(wallet.Id);
        Assert.NotNull(stored);
        Assert.Equal(0m, stored.Balance.Amount);
    }
}
