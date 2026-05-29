using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using MassTransit;

namespace BankingSystem.Infrastructure.Messaging.Consumers;

public sealed class RollbackDebitConsumer(RollbackDebitSagaHandler handler)
    : IConsumer<RollbackDebitMessage>
{
    public async Task Consume(ConsumeContext<RollbackDebitMessage> context) =>
        await handler.HandleAsync(context.Message, context.CancellationToken);
}
