using BankingSystem.Application.Common;
using BankingSystem.Application.Transactions.Saga.Messages;
using BankingSystem.Application.Transactions.Transfer;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BankingSystem.UnitTests.Application.Transactions.Transfer;

public sealed class TransferHandlerTests
{
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<ITransactionRepository> _transactionRepoMock = new();
    private readonly Mock<IIdempotencyService> _idempotencyMock = new();
    private readonly Mock<IDistributedLockService> _lockMock = new();
    private readonly Mock<IMessageBus> _messageBusMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly TransferHandler _sut;

    private static readonly Mock<IAsyncDisposable> HeldLock = new();

    public TransferHandlerTests()
    {
        _sut = new TransferHandler(
            _accountRepoMock.Object,
            _transactionRepoMock.Object,
            _idempotencyMock.Object,
            _lockMock.Object,
            _messageBusMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<TransferHandler>.Instance);

        _idempotencyMock
            .Setup(i => i.GetCachedResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _lockMock
            .Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeldLock.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task Given_ValidTransfer_When_Handle_Then_ReturnsPendingTransaction()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var command = BuildCommand(fromAccount.Id, toAccount.Id, amount: 200m);

        SetupAccounts(fromAccount, toAccount);
        SetupTransactionRepo();

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be("Pending");
        result.Value.TransactionId.Should().NotBeEmpty();
        result.Value.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Given_ValidTransfer_When_Handle_Then_PublishesInitiateSagaMessage()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var command = BuildCommand(fromAccount.Id, toAccount.Id, 300m);

        SetupAccounts(fromAccount, toAccount);
        SetupTransactionRepo();

        await _sut.HandleAsync(command, CancellationToken.None);

        _messageBusMock.Verify(
            m => m.PublishAsync(
                It.Is<InitiateTransferSagaMessage>(msg =>
                    msg.FromAccountId == fromAccount.Id &&
                    msg.ToAccountId == toAccount.Id &&
                    msg.Amount == 300m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Given_ValidTransfer_When_Handle_Then_PersistsTransaction()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var command = BuildCommand(fromAccount.Id, toAccount.Id, 100m);

        SetupAccounts(fromAccount, toAccount);
        SetupTransactionRepo();

        await _sut.HandleAsync(command, CancellationToken.None);

        _transactionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Given_ValidTransfer_When_Handle_Then_MarksIdempotencyKey()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var key = "my-idempotency-key";
        var command = BuildCommand(fromAccount.Id, toAccount.Id, idempotencyKey: key);

        SetupAccounts(fromAccount, toAccount);
        SetupTransactionRepo();

        await _sut.HandleAsync(command, CancellationToken.None);

        _idempotencyMock.Verify(
            i => i.MarkAsProcessedAsync(key, It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Given_DuplicateIdempotencyKey_When_Handle_Then_ReturnsCachedTransaction()
    {
        var existingTx = TestDataFactory.CreatePendingTransaction(idempotencyKey: "dup-key");
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount();

        _idempotencyMock
            .Setup(i => i.GetCachedResponseAsync("dup-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTx.Id.ToString());

        _transactionRepoMock
            .Setup(r => r.GetByIdempotencyKeyAsync("dup-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(existingTx));

        var command = BuildCommand(fromAccount.Id, toAccount.Id, idempotencyKey: "dup-key");

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeSuccess();
        result.Value.TransactionId.Should().Be(existingTx.Id);

        _messageBusMock.Verify(
            m => m.PublishAsync(It.IsAny<InitiateTransferSagaMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "saga must not be re-published for duplicate request");
    }

    [Fact]
    public async Task Given_InsufficientFunds_When_Handle_Then_ReturnsInsufficientFundsError()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 50m);
        var toAccount = TestDataFactory.CreateActiveAccount(balance: 0m);
        var command = BuildCommand(fromAccount.Id, toAccount.Id, amount: 100m);

        SetupAccounts(fromAccount, toAccount);

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeFailureWithCode("Account.InsufficientFunds");

        _transactionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _messageBusMock.Verify(
            m => m.PublishAsync(It.IsAny<InitiateTransferSagaMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Given_SourceAccountNotFound_When_Handle_Then_ReturnsNotFoundError()
    {
        var missingId = Guid.NewGuid();
        var toAccount = TestDataFactory.CreateActiveAccount();

        _accountRepoMock
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(nameof(Account), missingId));
        _accountRepoMock
            .Setup(r => r.GetByIdAsync(toAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(toAccount));

        var result = await _sut.HandleAsync(BuildCommand(missingId, toAccount.Id), CancellationToken.None);

        result.Should().BeFailure();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Given_CannotAcquireLock_When_Handle_Then_ReturnsLockFailedError()
    {
        _lockMock
            .Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable?)null);

        var result = await _sut.HandleAsync(BuildCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Should().BeFailureWithCode("Transfer.LockFailed");
    }

    [Fact]
    public async Task Given_ValidTransfer_When_Handle_Then_AcquiresTwoLocks()
    {
        var fromAccount = TestDataFactory.CreateActiveAccount(balance: 500m);
        var toAccount = TestDataFactory.CreateActiveAccount();
        var command = BuildCommand(fromAccount.Id, toAccount.Id);

        SetupAccounts(fromAccount, toAccount);
        SetupTransactionRepo();

        await _sut.HandleAsync(command, CancellationToken.None);

        _lockMock.Verify(
            l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Given_TwoTransfersWithSameAccounts_When_Handle_Then_LockKeysAreConsistentlyOrdered()
    {
        var idA = new Guid("00000000-0000-0000-0000-000000000001");
        var idB = new Guid("00000000-0000-0000-0000-000000000002");

        var capturedAtoB = new List<string>();
        var capturedBtoA = new List<string>();

        var sutAtoB = BuildHandlerWithLockCapture(capturedAtoB);
        var sutBtoA = BuildHandlerWithLockCapture(capturedBtoA);

        var accountA = TestDataFactory.CreateActiveAccount(1000m);
        var accountB = TestDataFactory.CreateActiveAccount(1000m);

        // Mock by idA/idB (the IDs used in commands), not by account.Id
        _accountRepoMock.Setup(r => r.GetByIdAsync(idA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(accountA));
        _accountRepoMock.Setup(r => r.GetByIdAsync(idB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(accountB));
        SetupTransactionRepo();

        await sutAtoB.HandleAsync(BuildCommand(idA, idB), CancellationToken.None);
        await sutBtoA.HandleAsync(BuildCommand(idB, idA), CancellationToken.None);

        capturedAtoB.Should().BeEquivalentTo(capturedBtoA,
            "lock ordering must be canonical to prevent deadlocks");
    }


    private TransferCommand BuildCommand(
        Guid? fromId = null,
        Guid? toId = null,
        decimal amount = 100m,
        string idempotencyKey = "test-key") =>
        new(fromId ?? Guid.NewGuid(), toId ?? Guid.NewGuid(), amount, "EUR", idempotencyKey);

    private void SetupAccounts(Account from, Account to)
    {
        _accountRepoMock
            .Setup(r => r.GetByIdAsync(from.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(from));
        _accountRepoMock
            .Setup(r => r.GetByIdAsync(to.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(to));
    }

    private void SetupTransactionRepo() =>
        _transactionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private TransferHandler BuildHandlerWithLockCapture(List<string> capturedKeys)
    {
        var lockMock = new Mock<IDistributedLockService>();
        lockMock
            .Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, CancellationToken>((key, _, _) => capturedKeys.Add(key))
            .ReturnsAsync(HeldLock.Object);

        return new TransferHandler(
            _accountRepoMock.Object,
            _transactionRepoMock.Object,
            _idempotencyMock.Object,
            lockMock.Object,
            _messageBusMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<TransferHandler>.Instance);
    }
}
