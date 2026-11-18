using PlayerWallet.Console.Application;
using PlayerWallet.Console.Domain;

namespace PlayerWallet.Application.Tests;

/// <summary>Verifies use-case coordination and failure handling.</summary>
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
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Balance?.Amount);
        Assert.Equal(10m, (await service.DepositAsync(id, 10)).Balance?.Amount);

        OperationResult bet = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.None, bet.Error);
        decimal payout = bet.Payout?.Amount ?? 0m;
        Assert.InRange(payout, 0m, 50m);
        Assert.Equal(10m - 5m + payout, bet.Balance?.Amount);

        decimal remaining = bet.Balance?.Amount ?? 0m;
        Assert.Equal(remaining - 5m, (await service.WithdrawAsync(id, 5)).Balance?.Amount);
    }

    /// <summary>Commits the stake and gross payout as one balance change.</summary>
    [Fact]
    public async Task BetCommitsTheGrossPayout()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 10);
        OperationResult result = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.None, result.Error);
        Assert.Equal(10m - 5m + (result.Payout?.Amount ?? 0m), result.Balance?.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Accepts a bet for the whole balance.</summary>
    [Fact]
    public async Task BetForTheWholeBalanceSucceeds()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 5);
        OperationResult result = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.None, result.Error);
        Assert.Equal(result.Payout?.Amount ?? 0m, result.Balance?.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Reports a missing wallet for every operation.</summary>
    [Fact]
    public async Task MissingWalletIsReportedForEveryOperation()
    {
        var service = new WalletService(new TestRepository());
        Guid id = Guid.NewGuid();
        Assert.Equal(WalletError.WalletNotFound, (await service.GetBalanceAsync(id)).Error);
        Assert.Equal(WalletError.WalletNotFound, (await service.DepositAsync(id, 5)).Error);
        Assert.Equal(WalletError.WalletNotFound, (await service.WithdrawAsync(id, 5)).Error);
        Assert.Equal(WalletError.WalletNotFound, (await service.PlaceBetAsync(id, 5)).Error);
    }

    /// <summary>Rejects an overdraft without changing the stored balance.</summary>
    [Fact]
    public async Task OverdraftLeavesTheBalanceUnchanged()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 10);
        OperationResult result = await service.WithdrawAsync(id, 11);
        Assert.Equal(WalletError.InsufficientFunds, result.Error);
        Assert.Equal(10m, result.Balance?.Amount);
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
        Assert.Equal(WalletError.InvalidAmount, (await service.DepositAsync(id, amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.WithdrawAsync(id, amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.PlaceBetAsync(id, amount)).Error);
        Assert.Equal(0, repository.Saves);
    }

    /// <summary>Rejects insufficient funds and missing wallets without saving.</summary>
    [Fact]
    public async Task RejectedBetsDoNotSave()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(WalletError.InsufficientFunds, (await service.PlaceBetAsync(id, 5)).Error);
        Assert.Equal(WalletError.InvalidBet, (await service.PlaceBetAsync(id, 11)).Error);
        Assert.Equal(WalletError.WalletNotFound, (await service.PlaceBetAsync(Guid.NewGuid(), 5)).Error);
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
        await service.DepositAsync(id, 100);
        Assert.Equal(WalletError.InvalidBet, (await service.PlaceBetAsync(id, stake)).Error);
        Assert.Equal(1, repository.Saves);
    }

    /// <summary>Does not retry or expose an uncommitted payout after a conflict.</summary>
    [Fact]
    public async Task ConflictDoesNotReroll()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 10);
        repository.Conflict = true;
        OperationResult result = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.ConcurrencyConflict, result.Error);
        Assert.Null(result.Payout);
        Assert.Equal(10m, result.Balance?.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Propagates unexpected persistence failures without changing stored state.</summary>
    [Fact]
    public async Task PersistenceFailureDoesNotLeakMutatedWallet()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository);
        Guid id = await service.CreateWalletAsync();
        repository.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => service.DepositAsync(id, 10));
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Balance?.Amount);
    }
}
