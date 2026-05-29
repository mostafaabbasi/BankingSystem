using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using MassTransit;

namespace BankingSystem.Infrastructure.Messaging.Consumers;

public sealed class CompleteTransferConsumer(CompleteTransferSagaHandler handler)
    : IConsumer<CompleteTransferMessage>
{
    public async Task Consume(ConsumeContext<CompleteTransferMessage> context) =>
        await handler.HandleAsync(context.Message, context.CancellationToken);
}
