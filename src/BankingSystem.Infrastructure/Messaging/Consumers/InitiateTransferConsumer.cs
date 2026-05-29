using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using MassTransit;

namespace BankingSystem.Infrastructure.Messaging.Consumers;

public sealed class InitiateTransferConsumer(InitiateTransferSagaHandler handler)
    : IConsumer<InitiateTransferSagaMessage>
{
    public async Task Consume(ConsumeContext<InitiateTransferSagaMessage> context) =>
        await handler.HandleAsync(context.Message, context.CancellationToken);
}
