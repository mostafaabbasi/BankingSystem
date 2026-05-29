using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Saga;

public sealed class CompleteTransferSagaHandler(
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ILogger<CompleteTransferSagaHandler> logger)
{
    public async Task HandleAsync(CompleteTransferMessage message, CancellationToken ct)
    {
        logger.LogInformation("[SAGA] Completing transaction {Id}", message.TransactionId);

        var txResult = await transactionRepository.GetByIdAsync(message.TransactionId, ct);
        if (txResult.IsFailure) return;

        txResult.Value.MarkCompleted();
        await transactionRepository.UpdateAsync(txResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("[SAGA] Transfer completed successfully. TransactionId: {Id}", message.TransactionId);
    }
}
