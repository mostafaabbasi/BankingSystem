using System.Text.Json;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Outbox;
using BankingSystem.Domain.Transactions;
using BankingSystem.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Persistence;

public sealed class BankingDbContext(
    DbContextOptions<BankingDbContext> options,
    ILogger<BankingDbContext> logger) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutboxMessages();

        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private void ConvertDomainEventsToOutboxMessages()
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count != 0)
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        if (domainEvents.Count == 0) return;

        logger.LogDebug("Converting {Count} domain event(s) to outbox messages", domainEvents.Count);

        foreach (var domainEvent in domainEvents)
        {
            var typeName = domainEvent.GetType().AssemblyQualifiedName!;
            var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);

            var outboxMessage = new OutboxMessage(
                type: typeName,
                payload: payload,
                correlationId: domainEvent.EventId.ToString());

            OutboxMessages.Add(outboxMessage);
        }
    }
}
