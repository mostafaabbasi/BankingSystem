using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Accounts;

public sealed record AccountCreatedEvent(
    Guid AccountId,
    string OwnerName,
    Currency Currency,
    decimal InitialBalance) : DomainEvent;

public sealed record AccountDebitedEvent(
    Guid AccountId,
    Guid TransactionId,
    decimal Amount,
    decimal NewBalance) : DomainEvent;

public sealed record AccountCreditedEvent(
    Guid AccountId,
    Guid TransactionId,
    decimal Amount,
    decimal NewBalance) : DomainEvent;
