using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Transactions;

public sealed class Transaction : Entity
{
    private Transaction() { }

    private Transaction(
        Guid id,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        string? correlationId)
    {
        Id = id;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        Amount = amount;
        Currency = currency;
        Status = TransactionStatus.Pending;
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        CreatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new TransactionCreatedEvent(Id, FromAccountId, ToAccountId, Amount, Currency));
    }

    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public TransactionStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public string CorrelationId { get; private set; } = default!;
    public string? FailureReason { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static Result<Transaction> Create(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        string? correlationId = null)
    {
        if (fromAccountId == toAccountId)
            return Error.Validation("Transaction.SameAccount", "Cannot transfer to the same account.");

        if (amount <= 0)
            return Error.Validation("Transaction.InvalidAmount", "Transfer amount must be positive.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Error.Validation("Transaction.MissingIdempotencyKey", "Idempotency key is required.");

        return new Transaction(Guid.NewGuid(), fromAccountId, toAccountId, amount, currency, idempotencyKey, correlationId);
    }

    public Result MarkCompleted()
    {
        if (Status != TransactionStatus.Pending)
            return Error.Business("Transaction.InvalidTransition",
                $"Cannot complete transaction in status '{Status}'.");

        Status = TransactionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new TransactionCompletedEvent(Id, FromAccountId, ToAccountId, Amount));
        return Result.Success();
    }

    public Result MarkFailed(string reason)
    {
        if (Status is TransactionStatus.Completed or TransactionStatus.Compensated)
            return Error.Business("Transaction.InvalidTransition",
                $"Cannot fail transaction in status '{Status}'.");

        Status = TransactionStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new TransactionFailedEvent(Id, FromAccountId, ToAccountId, Amount, reason));
        return Result.Success();
    }

    public Result MarkCompensated()
    {
        if (Status != TransactionStatus.Failed)
            return Error.Business("Transaction.InvalidTransition",
                "Can only compensate a failed transaction.");

        Status = TransactionStatus.Compensated;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new TransactionCompensatedEvent(Id, FromAccountId, ToAccountId, Amount));
        return Result.Success();
    }
}
