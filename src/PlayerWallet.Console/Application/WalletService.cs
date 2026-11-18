using PlayerWallet.Console.Domain;

namespace PlayerWallet.Console.Application;

/// <summary>Coordinates idempotent wallet operations against an atomic store.</summary>
/// <remarks>
/// Each mutating method hands the store one callback instead of a load/save pair. The store
/// decides how to make that callback atomic, so moving to a database changes the store only.
/// </remarks>
public sealed class WalletService(IWalletRepository repository)
{
    private const string DuplicateWalletMessage = "A new wallet identifier already exists.";
    private const string MissingWalletMessage = "The wallet does not exist.";

    private readonly IWalletRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Creates a new empty wallet for a session.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifier of the created wallet.</returns>
    public async Task<Guid> CreateWalletAsync(CancellationToken cancellationToken = default)
    {
        var wallet = new Wallet(Guid.CreateVersion7(), default);
        if (!await _repository.CreateAsync(wallet, cancellationToken))
        {
            throw new InvalidOperationException(DuplicateWalletMessage);
        }

        return wallet.Id;
    }

    /// <summary>Returns the current wallet balance.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current balance.</returns>
    public async Task<Money> GetBalanceAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).Balance;

    /// <summary>Deposits a positive cent-precision amount at most once per operation identifier.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="operationId">The identifier that makes this operation safe to replay.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed result, or the result recorded for a replay.</returns>
    public async Task<OperationResult> DepositAsync(
        Guid id,
        Guid operationId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (!Money.IsValid(amount) || amount is 0)
        {
            return await RejectAsync(id, WalletError.InvalidAmount, cancellationToken);
        }

        var credit = new Money(amount);
        return await _repository.ExecuteAsync(
            id,
            operationId,
            wallet => wallet.Deposit(credit) is { } error
                ? OperationResult.Rejected(error, wallet.Balance)
                : OperationResult.Success(wallet.Balance),
            cancellationToken);
    }

    /// <summary>Withdraws a positive amount at most once per operation identifier.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="operationId">The identifier that makes this operation safe to replay.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed result, or the result recorded for a replay.</returns>
    public async Task<OperationResult> WithdrawAsync(
        Guid id,
        Guid operationId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (!Money.IsValid(amount) || amount is 0)
        {
            return await RejectAsync(id, WalletError.InvalidAmount, cancellationToken);
        }

        var debit = new Money(amount);
        return await _repository.ExecuteAsync(
            id,
            operationId,
            wallet => wallet.Withdraw(debit) is { } error
                ? OperationResult.Rejected(error, wallet.Balance)
                : OperationResult.Success(wallet.Balance),
            cancellationToken);
    }

    /// <summary>Plays and commits one round at most once per operation identifier.</summary>
    /// <param name="id">The wallet identifier.</param>
    /// <param name="operationId">The identifier that makes this operation safe to replay.</param>
    /// <param name="amount">The stake, within the game limits.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The committed result, or the result recorded for a replay.</returns>
    public async Task<OperationResult> PlaceBetAsync(
        Guid id,
        Guid operationId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (!Money.IsValid(amount) || amount is 0)
        {
            return await RejectAsync(id, WalletError.InvalidAmount, cancellationToken);
        }

        var stake = new Money(amount);

        // The limit check runs before the draw because Play rejects stakes it cannot price.
        // Funds are checked again inside the store, against the balance it commits.
        if (!SlotGame.IsStakeWithinLimits(stake.Amount))
        {
            return await RejectAsync(id, WalletError.InvalidBet, cancellationToken);
        }

        // Drawn once, outside the store: the store never replays the callback, and no lock or
        // transaction is held while the game runs. A caller retrying with the same operation
        // identifier gets the recorded payout instead of a second draw.
        Money payout = SlotGame.Play(stake);
        return await _repository.ExecuteAsync(
            id,
            operationId,
            wallet => wallet.SettleBet(stake, payout) is { } error
                ? OperationResult.Rejected(error, wallet.Balance)
                : OperationResult.Success(wallet.Balance, payout),
            cancellationToken);
    }

    private async Task<OperationResult> RejectAsync(Guid id, WalletError error, CancellationToken cancellationToken) =>
        OperationResult.Rejected(error, (await LoadAsync(id, cancellationToken)).Balance);

    private async Task<Wallet> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _repository.GetAsync(id, cancellationToken) ?? throw new InvalidOperationException(MissingWalletMessage);
}
