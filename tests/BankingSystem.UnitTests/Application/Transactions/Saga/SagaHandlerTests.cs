using BankingSystem.Application.Common;
using BankingSystem.Application.Transactions.Saga;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BankingSystem.UnitTests.Application.Transactions.Saga;

public sealed class DebitAccountSagaHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepoMock = new();
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMessageBus> _messageBusMock = new();
    private readonly DebitAccountSagaHandler _sut;

    public DebitAccountSagaHandlerTests()
    {
        _sut = new DebitAccountSagaHandler(
            _txRepoMock.Object,
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            _messageBusMock.Object,
            NullLogger<DebitAccountSagaHandler>.Instance);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Given_SufficientFunds_When_HandleDebit_Then_PublishesCreditMessage()
    {
        var account = TestDataFactory.CreateActiveAccount(balance: 500m);
        var tx = TestDataFactory.CreatePendingTransaction(fromAccountId: account.Id, amount: 200m);

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(account));
        _accountRepoMock.Setup(r => r.UpdateAsync(account, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _txRepoMock
            .Setup(r => r.GetByIdAsync(tx.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(tx));

        var message = new DebitAccountMessage(tx.Id, account.Id, 200m, "corr-1");

        await _sut.HandleAsync(message, CancellationToken.None);

        _messageBusMock.Verify(
            m => m.PublishAsync(
                It.Is<CreditAccountMessage>(msg => msg.Amount == 200m),
                It.IsAny<CancellationToken>()),
            Times.Once);

        account.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task Given_InsufficientFunds_When_HandleDebit_Then_MarksTransactionFailed()
    {
        var account = TestDataFactory.CreateActiveAccount(balance: 50m);
        var tx = TestDataFactory.CreatePendingTransaction(fromAccountId: account.Id, amount: 200m);

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(account));

        _txRepoMock
            .Setup(r => r.GetByIdAsync(tx.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(tx));
        _txRepoMock.Setup(r => r.UpdateAsync(tx, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var message = new DebitAccountMessage(tx.Id, account.Id, 200m, "corr-1");

        await _sut.HandleAsync(message, CancellationToken.None);

        tx.Status.Should().Be(TransactionStatus.Failed);

        _messageBusMock.Verify(
            m => m.PublishAsync(It.IsAny<CreditAccountMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "credit must not be attempted when debit fails");
    }

    [Fact]
    public async Task Given_AccountNotFound_When_HandleDebit_Then_MarksTransactionFailed()
    {
        var tx = TestDataFactory.CreatePendingTransaction();
        var missingId = Guid.NewGuid();

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(nameof(Account), missingId));

        _txRepoMock
            .Setup(r => r.GetByIdAsync(tx.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(tx));
        _txRepoMock.Setup(r => r.UpdateAsync(tx, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var message = new DebitAccountMessage(tx.Id, missingId, 100m, "corr-1");

        await _sut.HandleAsync(message, CancellationToken.None);

        tx.Status.Should().Be(TransactionStatus.Failed);
    }
}

public sealed class CreditAccountSagaHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepoMock = new();
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMessageBus> _messageBusMock = new();
    private readonly CreditAccountSagaHandler _sut;

    public CreditAccountSagaHandlerTests()
    {
        _sut = new CreditAccountSagaHandler(
            _txRepoMock.Object,
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            _messageBusMock.Object,
            NullLogger<CreditAccountSagaHandler>.Instance);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Given_ActiveDestinationAccount_When_HandleCredit_Then_PublishesCompleteMessage()
    {
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var tx = TestDataFactory.CreatePendingTransaction(toAccountId: toAccount.Id, amount: 300m);

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(toAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(toAccount));
        _accountRepoMock.Setup(r => r.UpdateAsync(toAccount, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var message = new CreditAccountMessage(tx.Id, toAccount.Id, 300m, "corr-1");

        await _sut.HandleAsync(message, CancellationToken.None);

        _messageBusMock.Verify(
            m => m.PublishAsync(
                It.Is<CompleteTransferMessage>(msg => msg.TransactionId == tx.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        toAccount.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task Given_AccountNotFound_When_HandleCredit_Then_PublishesRollbackMessage()
    {
        var tx = TestDataFactory.CreatePendingTransaction(amount: 100m);
        var missingId = Guid.NewGuid();

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(nameof(Account), missingId));

        _txRepoMock
            .Setup(r => r.GetByIdAsync(tx.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(tx));

        var message = new CreditAccountMessage(tx.Id, missingId, 100m, "corr-1");

        await _sut.HandleAsync(message, CancellationToken.None);

        _messageBusMock.Verify(
            m => m.PublishAsync(
                It.Is<RollbackDebitMessage>(msg =>
                    msg.TransactionId == tx.Id &&
                    msg.Amount == 100m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public sealed class RollbackDebitSagaHandlerTests
{
    private readonly Mock<ITransactionRepository> _txRepoMock = new();
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RollbackDebitSagaHandler _sut;

    public RollbackDebitSagaHandlerTests()
    {
        _sut = new RollbackDebitSagaHandler(
            _txRepoMock.Object,
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<RollbackDebitSagaHandler>.Instance);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Given_FailedTransaction_When_Rollback_Then_RestoresBalanceAndMarksCompensated()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 200m);
        var tx = TestDataFactory.CreatePendingTransaction(fromAccountId: fromAccount.Id, amount: 100m);
        tx.MarkFailed("credit failed");

        _accountRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(fromAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(fromAccount));
        _accountRepoMock.Setup(r => r.UpdateAsync(fromAccount, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _txRepoMock
            .Setup(r => r.GetByIdAsync(tx.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(tx));
        _txRepoMock.Setup(r => r.UpdateAsync(tx, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var message = new RollbackDebitMessage(tx.Id, fromAccount.Id, 100m, "corr-1", "credit failed");

        await _sut.HandleAsync(message, CancellationToken.None);

        fromAccount.Balance.Should().Be(300m, "balance restored by credit back");
        tx.Status.Should().Be(TransactionStatus.Compensated);
    }
}
