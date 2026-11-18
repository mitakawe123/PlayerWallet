using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;
using PlayerWallet.Console.Infrastructure;

namespace PlayerWallet.Infrastructure.Tests;

/// <summary>Verifies snapshot isolation, atomic execution, and recorded results.</summary>
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
        Assert.Null((await service.DepositAsync(first, Guid.CreateVersion7(), 25m)).Error);
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
            Guid.CreateVersion7(),
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
                Guid.CreateVersion7(),
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

    /// <summary>Applies one operation identifier once even under concurrent replays.</summary>
    [Fact]
    public async Task ConcurrentReplaysApplyOnce()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        Guid operation = Guid.CreateVersion7();
        await repository.CreateAsync(wallet);

        Task<OperationResult>[] replays =
        [
            .. Enumerable.Range(0, 20).Select(_ => Task.Run(() => repository.ExecuteAsync(
                wallet.Id,
                operation,
                candidate =>
                {
                    candidate.Deposit(new Money(1));
                    return OperationResult.Success(candidate.Balance);
                }))),
        ];

        OperationResult[] results = await Task.WhenAll(replays);
        Assert.All(results, result => Assert.Equal(1m, result.Balance.Amount));
        Wallet? saved = await repository.GetAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(1m, saved.Balance.Amount);
    }

    /// <summary>Records a committed operation once and replays its result without changing state.</summary>
    [Fact]
    public async Task CommittedOperationsAreRecordedOnce()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), new Money(5));
        Guid operation = Guid.CreateVersion7();
        await repository.CreateAsync(wallet);

        OperationResult first = await repository.ExecuteAsync(
            wallet.Id,
            operation,
            candidate =>
            {
                candidate.Deposit(new Money(5));
                return OperationResult.Success(candidate.Balance);
            });
        OperationResult replay = await repository.ExecuteAsync(
            wallet.Id,
            operation,
            candidate =>
            {
                candidate.Deposit(new Money(5));
                return OperationResult.Success(candidate.Balance);
            });

        Assert.Equal(10m, first.Balance.Amount);
        Assert.Equal(first, replay);
        Wallet? stored = await repository.GetAsync(wallet.Id);
        Assert.NotNull(stored);
        Assert.Equal(10m, stored.Balance.Amount);
    }

    /// <summary>Records nothing for a rejection, so a later retry sees the current balance.</summary>
    [Fact]
    public async Task RejectedOperationsAreNotRecorded()
    {
        var repository = new InMemoryWalletRepository();
        var wallet = new Wallet(Guid.NewGuid(), default);
        Guid operation = Guid.CreateVersion7();
        await repository.CreateAsync(wallet);

        OperationResult rejected = await repository.ExecuteAsync(
            wallet.Id,
            operation,
            candidate => OperationResult.Rejected(WalletError.InsufficientFunds, candidate.Balance));
        Assert.Equal(WalletError.InsufficientFunds, rejected.Error);

        OperationResult retry = await repository.ExecuteAsync(
            wallet.Id,
            operation,
            candidate =>
            {
                candidate.Deposit(new Money(1));
                return OperationResult.Success(candidate.Balance);
            });
        Assert.Null(retry.Error);
        Assert.Equal(1m, retry.Balance.Amount);
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
            Guid.CreateVersion7(),
            candidate => OperationResult.Success(candidate.Balance)));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync(wallet, cancellation.Token));
        Assert.Null(await repository.GetAsync(wallet.Id));

        await repository.CreateAsync(wallet);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(wallet.Id, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ExecuteAsync(
            wallet.Id,
            Guid.CreateVersion7(),
            candidate => OperationResult.Success(candidate.Balance),
            cancellation.Token));

        Wallet? stored = await repository.GetAsync(wallet.Id);
        Assert.NotNull(stored);
        Assert.Equal(0m, stored.Balance.Amount);
    }
}
