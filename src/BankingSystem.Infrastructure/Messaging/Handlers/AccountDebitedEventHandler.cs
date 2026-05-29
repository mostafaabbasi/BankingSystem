using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging.Handlers;

public sealed class AccountDebitedEventHandler(
    ILogger<AccountDebitedEventHandler> logger)
    : IEventHandler<AccountDebitedEvent>
{
    public Task HandleAsync(AccountDebitedEvent @event, CancellationToken ct = default)
    {
        logger.LogDebug(
            "[EVENT] Account debited. AccountId: {AccountId}, TxId: {TxId}, " +
            "Amount: {Amount}, NewBalance: {Balance}",
            @event.AccountId, @event.TransactionId, @event.Amount, @event.NewBalance);

        return Task.CompletedTask;
    }
}
