using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging.Handlers;

public sealed class AccountCreditedEventHandler(
    ILogger<AccountCreditedEventHandler> logger)
    : IEventHandler<AccountCreditedEvent>
{
    public Task HandleAsync(AccountCreditedEvent @event, CancellationToken ct = default)
    {
        logger.LogDebug(
            "[EVENT] Account credited. AccountId: {AccountId}, TxId: {TxId}, " +
            "Amount: {Amount}, NewBalance: {Balance}",
            @event.AccountId, @event.TransactionId, @event.Amount, @event.NewBalance);

        return Task.CompletedTask;
    }
}
