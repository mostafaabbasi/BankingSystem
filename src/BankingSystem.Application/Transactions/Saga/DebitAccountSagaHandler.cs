using BankingSystem.Application.Common;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Saga;

public sealed class DebitAccountSagaHandler(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    IMessageBus messageBus,
    ILogger<DebitAccountSagaHandler> logger)
{
    public async Task HandleAsync(DebitAccountMessage message, CancellationToken ct)
    {
        logger.LogInformation("[SAGA] Debiting account {AccountId} for TransactionId: {TxId}",
            message.AccountId, message.TransactionId);

        var accountResult = await accountRepository.GetByIdForUpdateAsync(message.AccountId, ct);
        if (accountResult.IsFailure)
        {
            await FailTransactionAsync(message.TransactionId, accountResult.Error.Message, ct);
            return;
        }

        var debitResult = accountResult.Value.Debit(message.Amount, message.TransactionId);
        if (debitResult.IsFailure)
        {
            await FailTransactionAsync(message.TransactionId, debitResult.Error.Message, ct);
            return;
        }

        await accountRepository.UpdateAsync(accountResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("[SAGA] Debit successful. Publishing credit command.");

        var txResult = await transactionRepository.GetByIdAsync(message.TransactionId, ct);
        if (txResult.IsFailure) return;

        await messageBus.PublishAsync(
            new CreditAccountMessage(
                message.TransactionId,
                txResult.Value.ToAccountId,
                message.Amount,
                message.CorrelationId),
            ct);
    }

    private async Task FailTransactionAsync(Guid transactionId, string reason, CancellationToken ct)
    {
        logger.LogWarning("[SAGA] Debit failed for TransactionId: {TxId}. Reason: {Reason}", transactionId, reason);

        var txResult = await transactionRepository.GetByIdAsync(transactionId, ct);
        if (txResult.IsSuccess)
        {
            txResult.Value.MarkFailed(reason);
            await transactionRepository.UpdateAsync(txResult.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
