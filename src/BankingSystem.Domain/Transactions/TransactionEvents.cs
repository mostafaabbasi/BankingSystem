using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Transactions;

public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency) : DomainEvent;

public sealed record TransactionCompletedEvent(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount) : DomainEvent;

public sealed record TransactionFailedEvent(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Reason) : DomainEvent;

public sealed record TransactionCompensatedEvent(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount) : DomainEvent;
