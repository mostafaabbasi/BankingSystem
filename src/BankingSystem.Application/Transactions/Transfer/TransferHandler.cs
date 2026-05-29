using BankingSystem.Application.Common;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Transfer;

public sealed record TransferCommand(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string IdempotencyKey) : ICommand<Result<TransferResponse>>;

public sealed record TransferResponse(
    Guid TransactionId,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAt);

public sealed class TransferCommandValidator : AbstractValidator<TransferCommand>
{
    public TransferCommandValidator()
    {
        RuleFor(x => x.FromAccountId).NotEmpty();
        RuleFor(x => x.ToAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be positive.");
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(256);

        RuleFor(x => x)
            .Must(x => x.FromAccountId != x.ToAccountId)
            .WithMessage("Source and destination accounts must differ.");
    }
}

public sealed class TransferHandler(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IIdempotencyService idempotencyService,
    IDistributedLockService lockService,
    IMessageBus messageBus,
    IUnitOfWork unitOfWork,
    ILogger<TransferHandler> logger)
    : ICommandHandler<TransferCommand, Result<TransferResponse>>
{
    public async Task<Result<TransferResponse>> HandleAsync(
        TransferCommand command, CancellationToken ct = default)
    {
        var cached = await idempotencyService.GetCachedResponseAsync(command.IdempotencyKey, ct);
        if (cached is not null)
        {
            logger.LogInformation("Duplicate transfer request detected. Key: {Key}", command.IdempotencyKey);
            var duplicate = await transactionRepository.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct);
            if (duplicate.IsSuccess)
            {
                var t = duplicate.Value;
                return new TransferResponse(t.Id, t.Status.ToString(), t.CorrelationId, t.CreatedAt);
            }
        }

        var (lockKey1, lockKey2) = BuildLockKeys(command.FromAccountId, command.ToAccountId);

        await using var lock1 = await lockService.AcquireLockAsync(lockKey1, TimeSpan.FromSeconds(10), ct);
        if (lock1 is null)
            return Error.Business("Transfer.LockFailed", "Could not acquire lock on source account. Please retry.");

        await using var lock2 = await lockService.AcquireLockAsync(lockKey2, TimeSpan.FromSeconds(10), ct);
        if (lock2 is null)
            return Error.Business("Transfer.LockFailed", "Could not acquire lock on destination account. Please retry.");

        var fromResult = await accountRepository.GetByIdAsync(command.FromAccountId, ct);
        if (fromResult.IsFailure) return fromResult.Error;

        var toResult = await accountRepository.GetByIdAsync(command.ToAccountId, ct);
        if (toResult.IsFailure) return toResult.Error;

        if (fromResult.Value.Balance < command.Amount)
            return Error.Business("Account.InsufficientFunds",
                $"Insufficient funds. Available: {fromResult.Value.Balance}, Requested: {command.Amount}.");

        var transactionResult = Transaction.Create(
            command.FromAccountId, command.ToAccountId,
            command.Amount, command.Currency, command.IdempotencyKey);

        if (transactionResult.IsFailure) return transactionResult.Error;

        var transaction = transactionResult.Value;
        await transactionRepository.AddAsync(transaction, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await idempotencyService.MarkAsProcessedAsync(
            command.IdempotencyKey, transaction.Id.ToString(), TimeSpan.FromDays(7), ct);

        await messageBus.PublishAsync(
            new InitiateTransferSagaMessage(
                transaction.Id, transaction.FromAccountId, transaction.ToAccountId,
                transaction.Amount, transaction.Currency, transaction.CorrelationId),
            ct);

        logger.LogInformation(
            "Transfer saga initiated. TransactionId: {TransactionId}, CorrelationId: {CorrelationId}",
            transaction.Id, transaction.CorrelationId);

        return new TransferResponse(
            transaction.Id, transaction.Status.ToString(),
            transaction.CorrelationId, transaction.CreatedAt);
    }

    private static (string, string) BuildLockKeys(Guid a, Guid b)
    {
        var ids = new[] { a, b }.OrderBy(x => x).ToArray();
        return ($"account:lock:{ids[0]}", $"account:lock:{ids[1]}");
    }
}
