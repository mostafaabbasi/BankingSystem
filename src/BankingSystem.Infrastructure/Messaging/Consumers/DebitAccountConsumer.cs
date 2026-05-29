using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using MassTransit;

namespace BankingSystem.Infrastructure.Messaging.Consumers;

public sealed class DebitAccountConsumer(DebitAccountSagaHandler handler)
    : IConsumer<DebitAccountMessage>
{
    public async Task Consume(ConsumeContext<DebitAccountMessage> context) =>
        await handler.HandleAsync(context.Message, context.CancellationToken);
}
