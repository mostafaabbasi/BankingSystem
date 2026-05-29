using BankingSystem.Application.Common;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Messaging;

public sealed class RabbitMqMessageBus(
    IPublishEndpoint publishEndpoint,
    ILogger<RabbitMqMessageBus> logger) : IMessageBus
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        logger.LogDebug("Publishing message {MessageType}", typeof(T).Name);
        await publishEndpoint.Publish(message, ct);
    }
}
