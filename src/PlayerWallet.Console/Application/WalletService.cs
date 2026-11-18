using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Coordinates wallet operations and explicit persistence.</summary>
public sealed class WalletService(IWalletRepository repository)
{
    private const string DuplicateWalletMessage = "A new wallet identifier already exists.";
    private const string MissingWalletMessage = "The wallet does not exist.";
    private const string ConflictMessage = "The wallet changed before it could be saved.";

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
    public async Task<Money> GetBalanceAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).Balance;

    /// <summary>Deposits a positive cent-precision amount.</summary>
    public async Task<OperationResult> DepositAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet wallet = await LoadAsync(id, cancellationToken);
        if (!Money.IsValid(amount) || amount is 0)
        {
            return OperationResult.Rejected(WalletError.InvalidAmount, wallet.Balance);
        }

        if (wallet.Deposit(new Money(amount)) is { } error)
        {
            return OperationResult.Rejected(error, wallet.Balance);
        }

        await SaveAsync(wallet, cancellationToken);
        return OperationResult.Success(wallet.Balance);
    }

    /// <summary>Withdraws a positive amount without overdrawing the wallet.</summary>
    public async Task<OperationResult> WithdrawAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet wallet = await LoadAsync(id, cancellationToken);
        if (!Money.IsValid(amount) || amount is 0)
        {
            return OperationResult.Rejected(WalletError.InvalidAmount, wallet.Balance);
        }

        if (wallet.Withdraw(new Money(amount)) is { } error)
        {
            return OperationResult.Rejected(error, wallet.Balance);
        }

        await SaveAsync(wallet, cancellationToken);
        return OperationResult.Success(wallet.Balance);
    }

    /// <summary>Generates and commits one round, without retrying or rerolling on conflicts.</summary>
    public async Task<OperationResult> PlaceBetAsync(Guid id, decimal amount, CancellationToken cancellationToken = default)
    {
        Wallet wallet = await LoadAsync(id, cancellationToken);
        if (!Money.IsValid(amount) || amount is 0)
        {
            return OperationResult.Rejected(WalletError.InvalidAmount, wallet.Balance);
        }

        var stake = new Money(amount);
        if (wallet.ValidateBet(stake) is { } error)
        {
            return OperationResult.Rejected(error, wallet.Balance);
        }

        Money payout = SlotGame.Play(stake);
        if (wallet.SettleBet(stake, payout) is { } settlementError)
        {
            return OperationResult.Rejected(settlementError, wallet.Balance);
        }

        await SaveAsync(wallet, cancellationToken);
        return OperationResult.Success(wallet.Balance, payout);
    }

    private async Task<Wallet> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _repository.GetAsync(id, cancellationToken) ?? throw new InvalidOperationException(MissingWalletMessage);

    private async Task SaveAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        if (!await _repository.SaveAsync(wallet, cancellationToken))
        {
            throw new InvalidOperationException(ConflictMessage);
        }
    }
}
