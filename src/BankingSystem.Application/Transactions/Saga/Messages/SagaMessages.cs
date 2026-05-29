namespace BankingSystem.Application.Transactions.Saga.Messages;

public sealed record InitiateTransferSagaMessage(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string CorrelationId);

public sealed record DebitAccountMessage(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string CorrelationId);

public sealed record CreditAccountMessage(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string CorrelationId);

public sealed record CompleteTransferMessage(
    Guid TransactionId,
    string CorrelationId);

public sealed record RollbackDebitMessage(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string CorrelationId,
    string Reason);
