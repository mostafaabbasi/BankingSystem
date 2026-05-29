using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging.Handlers;

public sealed class AccountCreatedEventHandler(
    ILogger<AccountCreatedEventHandler> logger)
    : IEventHandler<AccountCreatedEvent>
{
    public Task HandleAsync(AccountCreatedEvent @event, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EVENT] Account created. AccountId: {AccountId}, Owner: {OwnerName}, " +
            "Currency: {Currency}, InitialBalance: {Balance}",
            @event.AccountId, @event.OwnerName, @event.Currency, @event.InitialBalance);

        return Task.CompletedTask;
    }
}
