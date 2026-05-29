using System.Text.Json;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Outbox;

public sealed class OutboxProcessorJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorJob> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in outbox processor. Will retry after interval.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("Outbox processor stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.BankingDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var messages = await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        logger.LogDebug("Processing {Count} outbox message(s)", messages.Count);

        foreach (var message in messages)
            await ProcessMessageAsync(message, dispatcher, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessMessageAsync(OutboxMessage message, IDispatcher dispatcher, CancellationToken ct)
    {
        try
        {
            var eventType = Type.GetType(message.Type);
            if (eventType is null)
            {
                logger.LogError("Cannot resolve type '{Type}' for outbox message {Id}. Dead-lettering.",
                    message.Type, message.Id);
                message.MarkFailed($"Type not found: {message.Type}");
                return;
            }

            var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType, JsonOptions) as IDomainEvent;
            if (domainEvent is null)
            {
                logger.LogError("Deserialization returned null for outbox message {Id}. Dead-lettering.", message.Id);
                message.MarkFailed("Deserialization returned null.");
                return;
            }

            await dispatcher.PublishAsync((object)domainEvent, ct);

            message.MarkProcessed();

            logger.LogDebug("Outbox message {Id} ({Type}) published successfully.", message.Id, eventType.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process outbox message {Id} (attempt {Attempt}). Will retry.",
                message.Id, message.RetryCount + 1);
            message.MarkFailed(ex.Message);
        }
    }
}