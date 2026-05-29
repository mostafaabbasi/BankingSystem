using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using MassTransit;

namespace BankingSystem.Infrastructure.Messaging.Consumers;

public sealed class CreditAccountConsumer(CreditAccountSagaHandler handler)
    : IConsumer<CreditAccountMessage>
{
    public async Task Consume(ConsumeContext<CreditAccountMessage> context) =>
        await handler.HandleAsync(context.Message, context.CancellationToken);
}
