using PlayerWallet.Domain;

namespace PlayerWallet.Application.Tests;

/// <summary>Verifies use-case coordination and failure handling.</summary>
public sealed class WalletServiceTests
{
    /// <summary>Creates an empty wallet and persists successful operations.</summary>
    [Fact]
    public async Task SessionOperationsPersistExpectedBalances()
    {
        var repository = new TestRepository();
        var random = new TestRandom();
        var service = new WalletService(repository, new SlotGame(new GameRules(), random));
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Balance?.Amount);
        Assert.Equal(10m, (await service.DepositAsync(id, 10)).Balance?.Amount);
        OperationResult bet = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.None, bet.Error);
        Assert.Equal(35.35m, bet.Payout?.Amount);
        Assert.Equal(40.35m, bet.Balance?.Amount);
        Assert.Equal(0m, (await service.WithdrawAsync(id, 40.35m)).Balance?.Amount);
    }

    /// <summary>Returns no payout when the round is a loss.</summary>
    [Fact]
    public async Task LosingBetReturnsNoPayout()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository, new SlotGame(new GameRules(), new TestRandom(category: 0)));
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 10);
        OperationResult result = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.None, result.Error);
        Assert.Null(result.Payout);
        Assert.Equal(5m, result.Balance?.Amount);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Rejects invalid amounts without saving or consuming randomness.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public async Task InvalidAmountsDoNotSave(decimal amount)
    {
        var repository = new TestRepository();
        var random = new TestRandom();
        var service = new WalletService(repository, new SlotGame(new GameRules(), random));
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(WalletError.InvalidAmount, (await service.DepositAsync(id, amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.WithdrawAsync(id, amount)).Error);
        Assert.Equal(WalletError.InvalidAmount, (await service.PlaceBetAsync(id, amount)).Error);
        Assert.Equal(0, repository.Saves);
        Assert.Equal(0, random.Calls);
    }

    /// <summary>Rejects insufficient funds and missing wallets before generating a round.</summary>
    [Fact]
    public async Task RejectedBetsDoNotConsumeRandomness()
    {
        var repository = new TestRepository();
        var random = new TestRandom();
        var service = new WalletService(repository, new SlotGame(new GameRules(), random));
        Guid id = await service.CreateWalletAsync();
        Assert.Equal(WalletError.InsufficientFunds, (await service.PlaceBetAsync(id, 5)).Error);
        Assert.Equal(WalletError.InvalidBet, (await service.PlaceBetAsync(id, 11)).Error);
        Assert.Equal(WalletError.WalletNotFound, (await service.PlaceBetAsync(Guid.NewGuid(), 5)).Error);
        Assert.Equal(0, random.Calls);
        Assert.Equal(0, repository.Saves);
    }

    /// <summary>Does not retry, reroll, or expose an uncommitted payout after a conflict.</summary>
    [Fact]
    public async Task ConflictDoesNotReroll()
    {
        var repository = new TestRepository();
        var random = new TestRandom();
        var service = new WalletService(repository, new SlotGame(new GameRules(), random));
        Guid id = await service.CreateWalletAsync();
        await service.DepositAsync(id, 10);
        repository.Conflict = true;
        OperationResult result = await service.PlaceBetAsync(id, 5);
        Assert.Equal(WalletError.ConcurrencyConflict, result.Error);
        Assert.Null(result.Payout);
        Assert.Equal(10m, result.Balance?.Amount);
        Assert.Equal(2, random.Calls);
        Assert.Equal(2, repository.Saves);
    }

    /// <summary>Propagates unexpected persistence failures without changing stored state.</summary>
    [Fact]
    public async Task PersistenceFailureDoesNotLeakMutatedWallet()
    {
        var repository = new TestRepository();
        var service = new WalletService(repository, new SlotGame(new GameRules(), new TestRandom()));
        Guid id = await service.CreateWalletAsync();
        repository.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => service.DepositAsync(id, 10));
        Assert.Equal(0m, (await service.GetBalanceAsync(id)).Balance?.Amount);
    }
}
