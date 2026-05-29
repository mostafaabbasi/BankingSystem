using BankingSystem.Application.Common;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Saga;

public sealed class InitiateTransferSagaHandler(
    ITransactionRepository transactionRepository,
    IMessageBus messageBus,
    ILogger<InitiateTransferSagaHandler> logger)
{
    public async Task HandleAsync(InitiateTransferSagaMessage message, CancellationToken ct)
    {
        logger.LogInformation("[SAGA] Initiating transfer. TransactionId: {Id}", message.TransactionId);

        var txResult = await transactionRepository.GetByIdAsync(message.TransactionId, ct);
        if (txResult.IsFailure)
        {
            logger.LogError("[SAGA] Transaction not found: {Id}", message.TransactionId);
            return;
        }

        await messageBus.PublishAsync(
            new DebitAccountMessage(
                message.TransactionId,
                message.FromAccountId,
                message.Amount,
                message.CorrelationId),
            ct);
    }
}
