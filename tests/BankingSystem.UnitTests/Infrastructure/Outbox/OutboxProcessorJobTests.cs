using System.Text.Json;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Outbox;
using BankingSystem.Infrastructure.Outbox;
using BankingSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BankingSystem.UnitTests.Infrastructure.Outbox;

public sealed class OutboxProcessorJobTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Mock<IDispatcher> _dispatcherMock = new();

    public OutboxProcessorJobTests()
    {
        var services = new ServiceCollection();

        // Name computed ONCE here — if inside the lambda, Guid.NewGuid() is called per
        // scope and each DbContext gets a separate InMemory database, breaking data sharing.
        var dbName = $"OutboxTest_{Guid.NewGuid()}";
        services.AddDbContext<BankingDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));

        services.AddSingleton<IDispatcher>(_dispatcherMock.Object);
        services.AddLogging();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.EnsureCreated();
    }

    public void Dispose() => _provider.Dispose();

    private OutboxProcessorJob CreateJob() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessorJob>.Instance);

    private async Task<int> CountAllAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.OutboxMessages.CountAsync();
    }

    private async Task<int> CountPendingAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .CountAsync();
    }

    private async Task<OutboxMessage?> GetFirstMessageAsync()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.OutboxMessages.FirstOrDefaultAsync();
    }

    private async Task SeedOutboxMessageAsync(OutboxMessage message)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
    }

    private static OutboxMessage PendingAccountCreatedEvent() =>
        new(
            type: typeof(AccountCreatedEvent).AssemblyQualifiedName!,
            payload: JsonSerializer.Serialize(
                new AccountCreatedEvent(Guid.NewGuid(), "owner-1", Currency.EUR, 100m),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            correlationId: Guid.NewGuid().ToString());

    private static async Task RunJobOnceAsync(OutboxProcessorJob job)
    {
        await job.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await job.StopAsync(CancellationToken.None);
    }

[Fact]
    public async Task Given_PendingMessage_When_ProcessBatch_Then_DispatchesEventAndMarksProcessed()
    {
        var message = PendingAccountCreatedEvent();
        await SeedOutboxMessageAsync(message);

        // Verify seeding worked
        var totalCount = await CountAllAsync();
        totalCount.Should().Be(1, "message must be persisted to InMemory after seeding");

        var pendingCount = await CountPendingAsync();
        pendingCount.Should().Be(1, "WHERE(Status == Pending) must find the seeded pending message");

        _dispatcherMock
            .Setup(d => d.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await RunJobOnceAsync(CreateJob());

        _dispatcherMock.Verify(
            d => d.PublishAsync(
                It.Is<object>(e => e.GetType() == typeof(AccountCreatedEvent)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var saved = await GetFirstMessageAsync();
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(OutboxMessageStatus.Processed);
        saved.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_NoPendingMessages_When_ProcessBatch_Then_DispatcherNeverCalled()
    {
        await RunJobOnceAsync(CreateJob());

        _dispatcherMock.Verify(
            d => d.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Given_DispatcherThrows_When_ProcessMessage_Then_MessageRemainsForRetry()
    {
        var message = PendingAccountCreatedEvent();
        await SeedOutboxMessageAsync(message);

        var pendingCount = await CountPendingAsync();
        pendingCount.Should().Be(1, "seeding must work before testing retry behaviour");

        _dispatcherMock
            .Setup(d => d.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler unavailable"));

        await RunJobOnceAsync(CreateJob());

        var saved = await GetFirstMessageAsync();
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(OutboxMessageStatus.Pending);
        saved.RetryCount.Should().Be(1);
        saved.Error.Should().Contain("Handler unavailable");
    }

    [Fact]
    public void Given_MessageAt4Retries_When_MarkFailedOnce_Then_IsDeadLettered()
    {
        var message = PendingAccountCreatedEvent();
        message.MarkFailed("e1");
        message.MarkFailed("e2");
        message.MarkFailed("e3");
        message.MarkFailed("e4");

        message.MarkFailed("final error");

        message.Status.Should().Be(OutboxMessageStatus.DeadLetter);
        message.RetryCount.Should().Be(5);
    }

    [Fact]
    public async Task Given_UnresolvableType_When_ProcessMessage_Then_MarksFailedWithoutDispatching()
    {
        var message = new OutboxMessage(
            type: "Some.NonExistent.Type, FakeAssembly",
            payload: "{}",
            correlationId: Guid.NewGuid().ToString());
        await SeedOutboxMessageAsync(message);

        var pendingCount = await CountPendingAsync();
        pendingCount.Should().Be(1, "seeding must work before testing dead-letter behaviour");

        await RunJobOnceAsync(CreateJob());

        _dispatcherMock.Verify(
            d => d.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var saved = await GetFirstMessageAsync();
        saved.Should().NotBeNull();
        saved!.Status.Should().NotBe(OutboxMessageStatus.Processed);
        saved.Error.Should().Contain("Type not found");
    }

    [Fact]
    public async Task Given_AlreadyProcessedMessage_When_ProcessBatch_Then_SkippedCompletely()
    {
        var message = PendingAccountCreatedEvent();
        message.MarkProcessed();
        await SeedOutboxMessageAsync(message);

        await RunJobOnceAsync(CreateJob());

        _dispatcherMock.Verify(
            d => d.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
