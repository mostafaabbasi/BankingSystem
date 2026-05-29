namespace BankingSystem.Infrastructure.Messaging.IntegrationEvents;

public sealed record TransferCompletedIntegrationEvent(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount);
