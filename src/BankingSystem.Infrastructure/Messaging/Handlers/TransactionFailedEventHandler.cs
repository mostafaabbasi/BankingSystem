using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging.Handlers;

public sealed class TransactionFailedEventHandler(
    ILogger<TransactionFailedEventHandler> logger)
    : IEventHandler<TransactionFailedEvent>
{
    public Task HandleAsync(TransactionFailedEvent @event, CancellationToken ct = default)
    {
        logger.LogWarning(
            "[EVENT] Transaction failed. TxId: {TxId}, Reason: {Reason}",
            @event.TransactionId, @event.Reason);

        return Task.CompletedTask;
    }
}
