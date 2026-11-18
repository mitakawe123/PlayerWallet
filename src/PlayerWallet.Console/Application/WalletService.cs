using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Coordinates wallet operations and explicit persistence.</summary>
public sealed class WalletService(IWalletRepository repository)
{
    private const string DuplicateWalletMessage = "A new wallet identifier already exists.";

    private readonly IWalletRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Creates a new empty wallet for a session.</summary>
    public async Task<Guid> CreateWalletAsync(CancellationToken cancellationToken = default)
    {
        var wallet = new Wallet(Guid.NewGuid(), default);
        if (!await _repository.CreateAsync(wallet, cancellationToken))
        {
            throw new InvalidOperationException(DuplicateWalletMessage);
        }

        return wallet.Id;
    }

    /// <summary>Returns the current wallet balance.</summary>
    public async Task<OperationResult> GetBalanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Wallet? wallet = await _repository.GetAsync(id, cancellationToken);
        return wallet is null
            ? new OperationResult(WalletError.WalletNotFound, null)
            : new OperationResult(WalletError.None, wallet.Balance);
    }

    /// <summary>Deposits a positive cent-precision amount.</summary>
    public async Task<OperationResult> DepositAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet? wallet = await _repository.GetAsync(id, cancellationToken);
        if (wallet is null)
        {
            return new OperationResult(WalletError.WalletNotFound, null);
        }

        if (!Money.IsValid(amount) || amount is 0)
        {
            return new OperationResult(WalletError.InvalidAmount, wallet.Balance);
        }

        WalletError error = wallet.Deposit(new Money(amount));
        if (error is not WalletError.None)
        {
            return new OperationResult(error, wallet.Balance);
        }

        return await SaveAsync(wallet, cancellationToken);
    }

    /// <summary>Withdraws a positive amount without overdrawing the wallet.</summary>
    public async Task<OperationResult> WithdrawAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet? wallet = await _repository.GetAsync(id, cancellationToken);
        if (wallet is null)
        {
            return new OperationResult(WalletError.WalletNotFound, null);
        }

        if (!Money.IsValid(amount) || amount is 0)
        {
            return new OperationResult(WalletError.InvalidAmount, wallet.Balance);
        }

        WalletError error = wallet.Withdraw(new Money(amount));
        if (error is not WalletError.None)
        {
            return new OperationResult(error, wallet.Balance);
        }

        return await SaveAsync(wallet, cancellationToken);
    }

    /// <summary>Generates and commits one round, without retrying or rerolling on conflicts.</summary>
    public async Task<OperationResult> PlaceBetAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet? wallet = await _repository.GetAsync(id, cancellationToken);
        if (wallet is null)
        {
            return new OperationResult(WalletError.WalletNotFound, null);
        }

        if (!Money.IsValid(amount) || amount is 0)
        {
            return new OperationResult(WalletError.InvalidAmount, wallet.Balance);
        }

        var stake = new Money(amount);
        WalletError error = wallet.ValidateBet(stake);
        if (error is not WalletError.None)
        {
            return new OperationResult(error, wallet.Balance);
        }

        Money payout = SlotGame.Play(stake);
        error = wallet.SettleBet(stake, payout);
        if (error is not WalletError.None)
        {
            return new OperationResult(error, wallet.Balance);
        }

        return await SaveAsync(wallet, cancellationToken, payout.Amount is 0 ? null : payout);
    }

    private async Task<OperationResult> SaveAsync(
        Wallet wallet,
        CancellationToken cancellationToken,
        Money? payout = null)
    {
        if (!await _repository.SaveAsync(wallet, cancellationToken))
        {
            OperationResult current = await GetBalanceAsync(wallet.Id, cancellationToken);
            return new OperationResult(WalletError.ConcurrencyConflict, current.Balance);
        }

        return new OperationResult(WalletError.None, wallet.Balance, payout);
    }
}
