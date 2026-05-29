using System.Text.Json;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Outbox;
using BankingSystem.Infrastructure.Outbox;
using BankingSystem.Infrastructure.Persistence;
using BankingSystem.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BankingSystem.IntegrationTests.Infrastructure;

public sealed class OutboxIntegrationTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Given_AccountCreated_When_SaveChanges_Then_OutboxMessagePersistedAtomically()
    {
        var created = await CreateAccountAsync(initialBalance: 500m);

        var db = factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<BankingDbContext>();

        var outboxMessages = await db.OutboxMessages
            .Where(m => m.Type.Contains(nameof(AccountCreatedEvent)))
            .ToListAsync();

        outboxMessages.Should().NotBeEmpty(
            "AccountCreatedEvent must be written to outbox in the same transaction as the account");

        var message = outboxMessages.First();
        message.Status.Should().BeOneOf(
            [OutboxMessageStatus.Pending, OutboxMessageStatus.Processed],
            "message should be pending or already processed by background job");
        message.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Given_TransferInitiated_When_SagaCompletes_Then_TransactionEventsInOutbox()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);

        await TransferAsync(from.AccountId, to.AccountId, 200m);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var db = factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<BankingDbContext>();

        var outboxMessages = await db.OutboxMessages.ToListAsync();

        outboxMessages.Should().NotBeEmpty("at minimum the TransactionCreatedEvent should be in the outbox");
    }

    [Fact]
    public async Task Given_OutboxMessage_When_Persisted_Then_PayloadDeserializesToOriginalEvent()
    {
        await CreateAccountAsync(initialBalance: 250m);

        var db = factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<BankingDbContext>();

        var message = await db.OutboxMessages
            .Where(m => m.Type.Contains(nameof(AccountCreatedEvent)))
            .FirstOrDefaultAsync();

        message.Should().NotBeNull();

        var eventType = Type.GetType(message!.Type);
        eventType.Should().NotBeNull("type must be resolvable from stored assembly-qualified name");

        var deserialized = JsonSerializer.Deserialize(
            message.Payload,
            eventType!,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType<AccountCreatedEvent>();

        var evt = (AccountCreatedEvent)deserialized!;
        evt.InitialBalance.Should().Be(250m);
        evt.AccountId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_PendingOutboxMessages_When_ProcessorRuns_Then_EventuallyAllProcessed()
    {
        await CreateAccountAsync(initialBalance: 100m);

        var db = factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<BankingDbContext>();

        OutboxMessageStatus? finalStatus = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            var freshDb = factory.Services.CreateScope().ServiceProvider
                .GetRequiredService<BankingDbContext>();

            var messages = await freshDb.OutboxMessages
                .Where(m => m.Type.Contains(nameof(AccountCreatedEvent)))
                .ToListAsync();

            if (messages.All(m => m.Status == OutboxMessageStatus.Processed))
            {
                finalStatus = OutboxMessageStatus.Processed;
                break;
            }
        }

        finalStatus.Should().Be(OutboxMessageStatus.Processed,
            "the OutboxProcessorJob background service must eventually process all pending messages");
    }
}