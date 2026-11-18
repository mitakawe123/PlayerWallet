using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Application.Tests;

/// <summary>Verifies use-case coordination, idempotency, and failure handling.</summary>
public sealed class WalletServiceTests
{
    /// <summary>Provides amounts that must be rejected.</summary>
    public static TheoryData<decimal> InvalidAmounts => new() { 0m, -1m, 0.001m };

    /// <summary>Provides stakes outside the inclusive game limits.</summary>
    public static TheoryData<decimal> InvalidStakes => new() { 0.99m, 10.01m };

    /// <summary>Creates an empty wallet and persists successful operations.</summary>
    [Fact]
    public async Task SessionOperationsPersistExpectedBalances()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Amount);
        Assert.Equal(10m, (await service.DepositAsync(id, Guid.CreateVersion7(), 10)).Balance.Amount);

        OperationResult bet = await service.PlaceBetAsync(id, Guid.CreateVersion7(), 5);
        Assert.Null(bet.Error);
        decimal payout = bet.Payout.Amount;
        Assert.InRange(payout, 0m, 50m);
        Assert.Equal(10m - 5m + payout, bet.Balance.Amount);

        decimal remaining = bet.Balance.Amount;
        Assert.Equal(remaining - 5m, (await service.WithdrawAsync(id, Guid.CreateVersion7(), 5)).Balance.Amount);
    }

    /// <summary>Commits the stake and gross payout as one balance change.</summary>
    [Fact]
    public async Task BetCommitsTheGrossPayout()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        OperationResult result = await service.PlaceBetAsync(id, Guid.CreateVersion7(), 5);
        Assert.Null(result.Error);
        Assert.Equal(10m - 5m + result.Payout.Amount, result.Balance.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Accepts a bet for the whole balance.</summary>
    [Fact]
    public async Task BetForTheWholeBalanceSucceeds()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 5);
        OperationResult result = await service.PlaceBetAsync(id, Guid.CreateVersion7(), 5);
        Assert.Null(result.Error);
        Assert.Equal(result.Payout.Amount, result.Balance.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Creates time-ordered wallet identifiers.</summary>
    [Fact]
    public async Task WalletIdentifiersAreVersion7()
    {
        var service = new WalletService(new TestRepository());
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(7, id.Version);
    }

    /// <summary>Reports no payout for deposits and withdrawals.</summary>
    [Fact]
    public async Task NonBetOperationsReportNoPayout()
    {
        var service = new WalletService(new TestRepository());
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(0m, (await service.DepositAsync(id, Guid.CreateVersion7(), 10)).Payout.Amount);
        Assert.Equal(0m, (await service.WithdrawAsync(id, Guid.CreateVersion7(), 5)).Payout.Amount);
    }

    /// <summary>Applies a replayed deposit once and returns the recorded result.</summary>
    [Fact]
    public async Task ReplayingADepositAppliesItOnce()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        Guid operation = Guid.CreateVersion7();

        OperationResult first = await service.DepositAsync(id, operation, 10);
        OperationResult replay = await service.DepositAsync(id, operation, 10);

        Assert.Equal(first, replay);
        Assert.Equal(10m, (await service.GetBalanceAsync(id)).Amount);
        Assert.Equal(1, repository.Saves);
    }

    /// <summary>Applies distinct operation identifiers separately.</summary>
    [Fact]
    public async Task DistinctOperationsApplySeparately()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        await service.DepositAsync(id, Guid.CreateVersion7(), 5);
        Assert.Equal(15m, (await service.GetBalanceAsync(id)).Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Returns the original payout when a bet is replayed.</summary>
    [Fact]
    public async Task ReplayingABetReturnsTheSamePayout()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        Guid operation = Guid.CreateVersion7();

        OperationResult first = await service.PlaceBetAsync(id, operation, 5);
        OperationResult replay = await service.PlaceBetAsync(id, operation, 5);

        Assert.Equal(first, replay);
        Assert.Equal(10m - 5m + first.Payout.Amount, (await service.GetBalanceAsync(id)).Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Does not record rejections, so a later retry sees the current balance.</summary>
    [Fact]
    public async Task RejectionsAreNotRecorded()
    {
        var service = new WalletService(new TestRepository());
        Guid id = await service.CreateWalletAsync();
        Guid operation = Guid.CreateVersion7();

        Assert.Equal(WalletError.InsufficientFunds, (await service.WithdrawAsync(id, operation, 10)).Error);
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        Assert.Null((await service.WithdrawAsync(id, operation, 10)).Error);
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Amount);
    }

    /// <summary>Records nothing when a commit fails, so a retry applies the operation once.</summary>
    [Fact]
    public async Task FailedCommitRecordsNothing()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        Guid operation = Guid.CreateVersion7();

        repository.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => service.PlaceBetAsync(id, operation, 5));
        repository.Fail = false;

        OperationResult retry = await service.PlaceBetAsync(id, operation, 5);
        Assert.Null(retry.Error);
        Assert.Equal(10m - 5m + retry.Payout.Amount, (await service.GetBalanceAsync(id)).Amount);
    }

    /// <summary>Treats a missing wallet as an unexpected failure rather than a user outcome.</summary>
    [Fact]
    public async Task MissingWalletIsAnUnexpectedFailure()
    {
        var service = new WalletService(new TestRepository());
        Guid id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetBalanceAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DepositAsync(id, Guid.CreateVersion7(), 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WithdrawAsync(id, Guid.CreateVersion7(), 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PlaceBetAsync(id, Guid.CreateVersion7(), 5));
    }

    /// <summary>Rejects an overdraft without changing the stored balance.</summary>
    [Fact]
    public async Task OverdraftLeavesTheBalanceUnchanged()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 10);
        OperationResult result = await service.WithdrawAsync(id, Guid.CreateVersion7(), 11);
        Assert.Equal(WalletError.InsufficientFunds, result.Error);
        Assert.Equal(10m, result.Balance.Amount);
        Assert.Equal(0m, result.Payout.Amount);
        Assert.Equal(1, repository.Saves);
    }

    /// <summary>Rejects invalid amounts without saving.</summary>
    [Theory]
    [MemberData(nameof(InvalidAmounts))]
    public async Task InvalidAmountsDoNotSave(decimal amount)
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(WalletError.InvalidAmount, (await service.DepositAsync(id, Guid.CreateVersion7(), amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.WithdrawAsync(id, Guid.CreateVersion7(), amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.PlaceBetAsync(id, Guid.CreateVersion7(), amount)).Error);
        Assert.Equal(0, repository.Saves);
    }

    /// <summary>Rejects insufficient funds and out-of-range stakes without saving.</summary>
    [Fact]
    public async Task RejectedBetsDoNotSave()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(WalletError.InsufficientFunds, (await service.PlaceBetAsync(id, Guid.CreateVersion7(), 5)).Error);
        Assert.Equal(WalletError.InvalidBet, (await service.PlaceBetAsync(id, Guid.CreateVersion7(), 11)).Error);
        Assert.Equal(0, repository.Saves);
    }

    /// <summary>Enforces the inclusive bet limits before playing.</summary>
    [Theory]
    [MemberData(nameof(InvalidStakes))]
    public async Task BetsOutsideTheLimitsAreRejected(decimal stake)
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, Guid.CreateVersion7(), 100);
        Assert.Equal(WalletError.InvalidBet, (await service.PlaceBetAsync(id, Guid.CreateVersion7(), stake)).Error);
        Assert.Equal(1, repository.Saves);
    }

    /// <summary>Propagates unexpected persistence failures without changing stored state.</summary>
    [Fact]
    public async Task PersistenceFailureDoesNotLeakMutatedWallet()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        repository.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => service.DepositAsync(id, Guid.CreateVersion7(), 10));
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Amount);
    }
}
