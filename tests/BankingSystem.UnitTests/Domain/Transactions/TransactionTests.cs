using BankingSystem.Domain.Transactions;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace BankingSystem.UnitTests.Domain.Transactions;

public sealed class TransactionTests
{

    public sealed class Create
    {
        [Fact]
        public void Given_ValidInputs_When_Create_Then_ReturnsPendingTransaction()
        {
            var fromId = Guid.NewGuid();
            var toId = Guid.NewGuid();

            var result = Transaction.Create(fromId, toId, 250m, "EUR", "idem-key-1");

            result.Should().BeSuccess();
            result.Value.Should().Match<Transaction>(t =>
                t.FromAccountId == fromId &&
                t.ToAccountId == toId &&
                t.Amount == 250m &&
                t.Currency == "EUR" &&
                t.Status == TransactionStatus.Pending &&
                t.IdempotencyKey == "idem-key-1" &&
                t.Id != Guid.Empty);
        }

        [Fact]
        public void Given_SameFromAndToAccount_When_Create_Then_ReturnsValidationError()
        {
            var id = Guid.NewGuid();

            var result = Transaction.Create(id, id, 100m, "EUR", "key-1");

            result.Should().BeFailureWithCode("Transaction.SameAccount");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999.99)]
        public void Given_NonPositiveAmount_When_Create_Then_ReturnsValidationError(decimal amount)
        {
            var result = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "EUR", "key-1");

            result.Should().BeFailureWithCode("Transaction.InvalidAmount");
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public void Given_EmptyIdempotencyKey_When_Create_Then_ReturnsValidationError(string? key)
        {
            var result = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", key!);

            result.Should().BeFailureWithCode("Transaction.MissingIdempotencyKey");
        }

        [Fact]
        public void Given_NewTransaction_When_Create_Then_RaisesTransactionCreatedEvent()
        {
            var fromId = Guid.NewGuid();
            var toId = Guid.NewGuid();

            var tx = Transaction.Create(fromId, toId, 100m, "USD", "key-1").Value;

            tx.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<TransactionCreatedEvent>()
                .Which.Should().Match<TransactionCreatedEvent>(e =>
                    e.TransactionId == tx.Id &&
                    e.FromAccountId == fromId &&
                    e.Amount == 100m);
        }

        [Fact]
        public void Given_NoCorrelationId_When_Create_Then_GeneratesCorrelationId()
        {
            var tx = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1").Value;

            tx.CorrelationId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Given_CustomCorrelationId_When_Create_Then_UsesProvidedCorrelationId()
        {
            var correlationId = "trace-abc-123";

            var tx = Transaction.Create(
                Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", "key-1",
                correlationId: correlationId).Value;

            tx.CorrelationId.Should().Be(correlationId);
        }
    }


    public sealed class MarkCompleted
    {
        [Fact]
        public void Given_PendingTransaction_When_MarkCompleted_Then_StatusIsCompleted()
        {
            var tx = TestDataFactory.CreatePendingTransaction();

            var result = tx.MarkCompleted();

            result.Should().BeSuccess();
            tx.Status.Should().Be(TransactionStatus.Completed);
            tx.CompletedAt.Should().NotBeNull()
                .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void Given_CompletedTransaction_When_MarkCompleted_Then_ReturnsError()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.MarkCompleted();

            var result = tx.MarkCompleted();

            result.Should().BeFailureWithCode("Transaction.InvalidTransition");
        }

        [Fact]
        public void Given_FailedTransaction_When_MarkCompleted_Then_ReturnsError()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.MarkFailed("some error");

            var result = tx.MarkCompleted();

            result.Should().BeFailureWithCode("Transaction.InvalidTransition");
        }

        [Fact]
        public void Given_PendingTransaction_When_MarkCompleted_Then_RaisesCompletedEvent()
        {
            var tx = TestDataFactory.CreatePendingTransaction(amount: 300m);
            tx.ClearDomainEvents();

            tx.MarkCompleted();

            tx.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<TransactionCompletedEvent>()
                .Which.Should().Match<TransactionCompletedEvent>(e =>
                    e.TransactionId == tx.Id &&
                    e.Amount == 300m);
        }
    }


    public sealed class MarkFailed
    {
        [Fact]
        public void Given_PendingTransaction_When_MarkFailed_Then_StatusIsFailedWithReason()
        {
            var tx = TestDataFactory.CreatePendingTransaction();

            var result = tx.MarkFailed("Insufficient funds");

            result.Should().BeSuccess();
            tx.Status.Should().Be(TransactionStatus.Failed);
            tx.FailureReason.Should().Be("Insufficient funds");
            tx.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void Given_CompletedTransaction_When_MarkFailed_Then_ReturnsError()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.MarkCompleted();

            var result = tx.MarkFailed("late failure");

            result.Should().BeFailureWithCode("Transaction.InvalidTransition");
            tx.Status.Should().Be(TransactionStatus.Completed, "status must not change");
        }

        [Fact]
        public void Given_FailedTransaction_When_MarkFailed_Then_RaisesTransactionFailedEvent()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.ClearDomainEvents();

            tx.MarkFailed("network error");

            tx.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<TransactionFailedEvent>()
                .Which.Reason.Should().Be("network error");
        }
    }


    public sealed class MarkCompensated
    {
        [Fact]
        public void Given_FailedTransaction_When_MarkCompensated_Then_StatusIsCompensated()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.MarkFailed("credit failed");

            var result = tx.MarkCompensated();

            result.Should().BeSuccess();
            tx.Status.Should().Be(TransactionStatus.Compensated);
        }

        [Fact]
        public void Given_PendingTransaction_When_MarkCompensated_Then_ReturnsError()
        {
            var tx = TestDataFactory.CreatePendingTransaction();

            var result = tx.MarkCompensated();

            result.Should().BeFailureWithCode("Transaction.InvalidTransition");
        }

        [Fact]
        public void Given_CompletedTransaction_When_MarkCompensated_Then_ReturnsError()
        {
            var tx = TestDataFactory.CreatePendingTransaction();
            tx.MarkCompleted();

            var result = tx.MarkCompensated();

            result.Should().BeFailureWithCode("Transaction.InvalidTransition");
        }

        [Fact]
        public void Given_FailedTransaction_When_MarkCompensated_Then_RaisesCompensatedEvent()
        {
            var tx = TestDataFactory.CreatePendingTransaction(amount: 150m);
            tx.MarkFailed("rollback");
            tx.ClearDomainEvents();

            tx.MarkCompensated();

            tx.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<TransactionCompensatedEvent>()
                .Which.Amount.Should().Be(150m);
        }
    }


    public sealed class LifecycleTests
    {
        [Fact]
        public void HappyPath_PendingToCompleted_LifecycleIsValid()
        {
            var tx = TestDataFactory.CreatePendingTransaction();

            tx.Status.Should().Be(TransactionStatus.Pending);
            tx.MarkCompleted().IsSuccess.Should().BeTrue();
            tx.Status.Should().Be(TransactionStatus.Completed);
        }

        [Fact]
        public void SagaFailure_PendingToFailedToCompensated_LifecycleIsValid()
        {
            var tx = TestDataFactory.CreatePendingTransaction();

            tx.MarkFailed("credit service unavailable").IsSuccess.Should().BeTrue();
            tx.MarkCompensated().IsSuccess.Should().BeTrue();

            tx.Status.Should().Be(TransactionStatus.Compensated);
            tx.FailureReason.Should().NotBeNullOrEmpty();
        }
    }
}
