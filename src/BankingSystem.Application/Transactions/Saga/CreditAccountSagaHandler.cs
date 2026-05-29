using BankingSystem.Application.Common;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Application.Transactions.Saga;

public sealed class CreditAccountSagaHandler(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    IMessageBus messageBus,
    ILogger<CreditAccountSagaHandler> logger)
{
    public async Task HandleAsync(CreditAccountMessage message, CancellationToken ct)
    {
        logger.LogInformation("[SAGA] Crediting account {AccountId} for TransactionId: {TxId}",
            message.AccountId, message.TransactionId);

        var accountResult = await accountRepository.GetByIdForUpdateAsync(message.AccountId, ct);
        if (accountResult.IsFailure)
        {
            logger.LogError("[SAGA] Credit failed. Initiating rollback. Reason: {Reason}",
                accountResult.Error.Message);

            var txResult = await transactionRepository.GetByIdAsync(message.TransactionId, ct);
            if (txResult.IsSuccess)
            {
                await messageBus.PublishAsync(
                    new RollbackDebitMessage(
                        message.TransactionId,
                        txResult.Value.FromAccountId,
                        message.Amount,
                        message.CorrelationId,
                        accountResult.Error.Message),
                    ct);
            }
            return;
        }

        accountResult.Value.Credit(message.Amount, message.TransactionId);
        await accountRepository.UpdateAsync(accountResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await messageBus.PublishAsync(
            new CompleteTransferMessage(message.TransactionId, message.CorrelationId),
            ct);
    }
}
