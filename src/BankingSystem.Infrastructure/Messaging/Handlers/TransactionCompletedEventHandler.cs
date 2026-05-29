using BankingSystem.Application.Common;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Transactions;
using BankingSystem.Infrastructure.Messaging.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging.Handlers;

public sealed class TransactionCompletedEventHandler(
    IMessageBus messageBus,
    ILogger<TransactionCompletedEventHandler> logger)
    : IEventHandler<TransactionCompletedEvent>
{
    public async Task HandleAsync(TransactionCompletedEvent @event, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EVENT] Transaction completed. TxId: {TxId}, From: {From}, To: {To}, Amount: {Amount}",
            @event.TransactionId, @event.FromAccountId, @event.ToAccountId, @event.Amount);

        await messageBus.PublishAsync(
            new TransferCompletedIntegrationEvent(
                @event.TransactionId, @event.FromAccountId, @event.ToAccountId, @event.Amount),
            ct);
    }
}
