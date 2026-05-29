using BankingSystem.Domain.Outbox;
using BankingSystem.Infrastructure.Outbox;
using FluentAssertions;
using Xunit;

namespace BankingSystem.UnitTests.Infrastructure.Outbox;

public sealed class OutboxMessageTests
{

    [Fact]
    public void Given_ValidArgs_When_Constructed_Then_IsPending()
    {
        var msg = new OutboxMessage("Some.Type", "{}", "corr-1");

        msg.Id.Should().NotBeEmpty();
        msg.Type.Should().Be("Some.Type");
        msg.Payload.Should().Be("{}");
        msg.CorrelationId.Should().Be("corr-1");
        msg.Status.Should().Be(OutboxMessageStatus.Pending);
        msg.RetryCount.Should().Be(0);
        msg.ProcessedAt.Should().BeNull();
        msg.Error.Should().BeNull();
        msg.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Given_TwoMessages_When_Constructed_Then_HaveDistinctIds()
    {
        var a = new OutboxMessage("T", "{}", "c1");
        var b = new OutboxMessage("T", "{}", "c2");

        a.Id.Should().NotBe(b.Id);
    }


    [Fact]
    public void Given_PendingMessage_When_MarkProcessed_Then_StatusIsProcessed()
    {
        var msg = new OutboxMessage("T", "{}", "c1");

        msg.MarkProcessed();

        msg.Status.Should().Be(OutboxMessageStatus.Processed);
        msg.ProcessedAt.Should().NotBeNull()
            .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }


    [Fact]
    public void Given_FirstFailure_When_MarkFailed_Then_StillPendingForRetry()
    {
        var msg = new OutboxMessage("T", "{}", "c1");

        msg.MarkFailed("timeout");

        msg.Status.Should().Be(OutboxMessageStatus.Pending,
            "message should remain Pending so it is retried");
        msg.RetryCount.Should().Be(1);
        msg.Error.Should().Be("timeout");
    }

    [Fact]
    public void Given_FourthFailure_When_MarkFailed_Then_StillPending()
    {
        var msg = new OutboxMessage("T", "{}", "c1");

        msg.MarkFailed("e1");
        msg.MarkFailed("e2");
        msg.MarkFailed("e3");
        msg.MarkFailed("e4");

        msg.RetryCount.Should().Be(4);
        msg.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public void Given_FifthFailure_When_MarkFailed_Then_DeadLettered()
    {
        var msg = new OutboxMessage("T", "{}", "c1");

        msg.MarkFailed("e1");
        msg.MarkFailed("e2");
        msg.MarkFailed("e3");
        msg.MarkFailed("e4");
        msg.MarkFailed("e5");

        msg.RetryCount.Should().Be(5);
        msg.Status.Should().Be(OutboxMessageStatus.DeadLetter,
            "after 5 failures the message must be dead-lettered to stop infinite retries");
    }

    [Fact]
    public void Given_FailedMessage_When_MarkFailed_Then_ErrorIsUpdatedToLatest()
    {
        var msg = new OutboxMessage("T", "{}", "c1");

        msg.MarkFailed("first error");
        msg.MarkFailed("second error");

        msg.Error.Should().Be("second error");
    }
}
