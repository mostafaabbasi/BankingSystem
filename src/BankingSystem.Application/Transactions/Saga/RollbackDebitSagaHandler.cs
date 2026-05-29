using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Saga;

public sealed class RollbackDebitSagaHandler(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    ILogger<RollbackDebitSagaHandler> logger)
{
    public async Task HandleAsync(RollbackDebitMessage message, CancellationToken ct)
    {
        logger.LogWarning("[SAGA] Rolling back debit for AccountId: {AccountId}, TransactionId: {TxId}",
            message.AccountId, message.TransactionId);

        var accountResult = await accountRepository.GetByIdForUpdateAsync(message.AccountId, ct);
        if (accountResult.IsSuccess)
        {
            accountResult.Value.Credit(message.Amount, message.TransactionId);
            await accountRepository.UpdateAsync(accountResult.Value, ct);
        }

        var txResult = await transactionRepository.GetByIdAsync(message.TransactionId, ct);
        if (txResult.IsSuccess)
        {
            txResult.Value.MarkFailed(message.Reason);
            txResult.Value.MarkCompensated();
            await transactionRepository.UpdateAsync(txResult.Value, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogWarning("[SAGA] Compensation completed for TransactionId: {Id}", message.TransactionId);
    }
}
