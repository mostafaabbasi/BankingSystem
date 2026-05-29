using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace BankingSystem.UnitTests.Domain.Accounts;

public sealed class AccountTests
{

    public sealed class Create
    {
        [Fact]
        public void Given_ValidInputs_When_Create_Then_ReturnsActiveAccount()
        {
            var result = Account.Create("owner-1", Currency.EUR, 500m);

            result.Should().BeSuccess()
                .Which.Should().Match<Account>(a =>
                    a.OwnerName == "owner-1" &&
                    a.Balance == 500m &&
                    a.Currency == Currency.EUR &&
                    a.Status == AccountStatus.Active &&
                    a.Id != Guid.Empty);
        }

        [Fact]
        public void Given_ZeroInitialBalance_When_Create_Then_Succeeds()
        {
            var result = Account.Create("owner-1", Currency.USD, 0m);
            result.Should().BeSuccess();
            result.Value.Balance.Should().Be(0m);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Given_InvalidOwnerName_When_Create_Then_ReturnsValidationError(string? ownerName)
        {
            var result = Account.Create(ownerName!, Currency.EUR, 100m);
            result.Should().BeFailureWithCode("Account.InvalidOwner");
        }

        [Fact]
        public void Given_NegativeInitialBalance_When_Create_Then_ReturnsValidationError()
        {
            var result = Account.Create("owner-1", Currency.EUR, -1m);
            result.Should().BeFailureWithCode("Account.NegativeBalance");
        }

        [Fact]
        public void Given_NewAccount_When_Created_Then_RaisesAccountCreatedDomainEvent()
        {
            var result = Account.Create("owner-1", Currency.EUR, 100m);

            result.Value.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<AccountCreatedEvent>()
                .Which.Should().Match<AccountCreatedEvent>(e =>
                    e.OwnerName == "owner-1" &&
                    e.Currency == Currency.EUR &&
                    e.InitialBalance == 100m);
        }

        [Fact]
        public void Given_TwoAccounts_When_Created_Then_HaveDistinctIds()
        {
            var a1 = Account.Create("owner-1", Currency.EUR).Value;
            var a2 = Account.Create("owner-2", Currency.EUR).Value;

            a1.Id.Should().NotBe(a2.Id);
        }
    }


    public sealed class Debit
    {
        [Fact]
        public void Given_SufficientBalance_When_Debit_Then_ReducesBalance()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 500m);
            var txId = Guid.NewGuid();

            var result = account.Debit(200m, txId);

            result.Should().BeSuccess();
            account.Balance.Should().Be(300m);
        }

        [Fact]
        public void Given_ExactBalance_When_Debit_Then_ZeroesBalance()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 100m);

            var result = account.Debit(100m, Guid.NewGuid());

            result.Should().BeSuccess();
            account.Balance.Should().Be(0m);
        }

        [Fact]
        public void Given_InsufficientFunds_When_Debit_Then_ReturnsError()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 50m);

            var result = account.Debit(100m, Guid.NewGuid());

            result.Should().BeFailureWithCode("Account.InsufficientFunds");
            account.Balance.Should().Be(50m, "balance must not change on failure");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-50)]
        public void Given_NonPositiveAmount_When_Debit_Then_ReturnsValidationError(decimal amount)
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 500m);

            var result = account.Debit(amount, Guid.NewGuid());

            result.Should().BeFailureWithCode("Account.InvalidAmount");
        }

        [Fact]
        public void Given_SuspendedAccount_When_Debit_Then_ReturnsNotActiveError()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 500m);
            account.Suspend();

            var result = account.Debit(100m, Guid.NewGuid());

            result.Should().BeFailureWithCode("Account.NotActive");
        }

        [Fact]
        public void Given_SuccessfulDebit_When_Debit_Then_RaisesAccountDebitedEvent()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 300m);
            account.ClearDomainEvents();
            var txId = Guid.NewGuid();

            account.Debit(100m, txId);

            account.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<AccountDebitedEvent>()
                .Which.Should().Match<AccountDebitedEvent>(e =>
                    e.AccountId == account.Id &&
                    e.TransactionId == txId &&
                    e.Amount == 100m &&
                    e.NewBalance == 200m);
        }

        [Fact]
        public void Given_SuccessfulDebit_When_Debit_Then_SetsUpdatedAt()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 200m);

            account.Debit(50m, Guid.NewGuid());

            account.UpdatedAt.Should().NotBeNull()
                .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        }
    }


    public sealed class Credit
    {
        [Fact]
        public void Given_ActiveAccount_When_Credit_Then_IncreasesBalance()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 100m);
            var txId = Guid.NewGuid();

            var result = account.Credit(250m, txId);

            result.Should().BeSuccess();
            account.Balance.Should().Be(350m);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Given_NonPositiveAmount_When_Credit_Then_ReturnsValidationError(decimal amount)
        {
            var account = TestDataFactory.CreateActiveAccount();

            var result = account.Credit(amount, Guid.NewGuid());

            result.Should().BeFailureWithCode("Account.InvalidAmount");
        }

        [Fact]
        public void Given_SuspendedAccount_When_Credit_Then_ReturnsNotActiveError()
        {
            var account = TestDataFactory.CreateActiveAccount();
            account.Suspend();

            var result = account.Credit(100m, Guid.NewGuid());

            result.Should().BeFailureWithCode("Account.NotActive");
        }

        [Fact]
        public void Given_SuccessfulCredit_When_Credit_Then_RaisesAccountCreditedEvent()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 0m);
            account.ClearDomainEvents();
            var txId = Guid.NewGuid();

            account.Credit(500m, txId);

            account.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<AccountCreditedEvent>()
                .Which.Should().Match<AccountCreditedEvent>(e =>
                    e.Amount == 500m &&
                    e.NewBalance == 500m &&
                    e.TransactionId == txId);
        }
    }


    public sealed class StatusTransitions
    {
        [Fact]
        public void Given_ActiveAccount_When_Suspend_Then_StatusIsSuspended()
        {
            var account = TestDataFactory.CreateActiveAccount();

            var result = account.Suspend();

            result.Should().BeSuccess();
            account.Status.Should().Be(AccountStatus.Suspended);
        }

        [Fact]
        public void Given_ClosedAccount_When_Suspend_Then_ReturnsError()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 0m);
            account.Close();

            var result = account.Suspend();

            result.Should().BeFailureWithCode("Account.AlreadyClosed");
        }

        [Fact]
        public void Given_ZeroBalance_When_Close_Then_StatusIsClosed()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 0m);

            var result = account.Close();

            result.Should().BeSuccess();
            account.Status.Should().Be(AccountStatus.Closed);
        }

        [Fact]
        public void Given_NonZeroBalance_When_Close_Then_ReturnsError()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 100m);

            var result = account.Close();

            result.Should().BeFailureWithCode("Account.NonZeroBalance");
        }
    }


    public sealed class DomainEventTests
    {
        [Fact]
        public void Given_MultipleOperations_When_ClearDomainEvents_Then_EventsAreEmpty()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 500m);
            account.Debit(100m, Guid.NewGuid());
            account.Credit(50m, Guid.NewGuid());

            account.ClearDomainEvents();

            account.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Given_MultipleOperations_When_DomainEventsRead_Then_AllEventsPresent()
        {
            var account = TestDataFactory.CreateActiveAccount(balance: 500m);
            // Create raises 1 event; debit raises another
            account.Debit(100m, Guid.NewGuid());

            account.DomainEvents.Should().HaveCount(2)
                .And.ContainItemsAssignableTo<AccountCreatedEvent>()
                .And.Contain(e => e is AccountDebitedEvent);
        }
    }
}
