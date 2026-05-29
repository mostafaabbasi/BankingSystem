using BankingSystem.Application.Accounts.CreateAccount;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankingSystem.UnitTests.Application.Accounts.CreateAccount;

public sealed class CreateAccountHandlerTests
{
    private readonly Mock<IAccountRepository> _accountRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateAccountHandler _sut;

    public CreateAccountHandlerTests()
    {
        _sut = new CreateAccountHandler(_accountRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Given_ValidCommand_When_Handle_Then_CreatesAccountAndReturnsResponse()
    {
        var command = new CreateAccountCommand("owner-123", "EUR", 500m);

        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeSuccess();
        result.Value.Should().Match<CreateAccountResponse>(r =>
            r.OwnerName == "owner-123" &&
            r.Balance == 500m &&
            r.Currency == "EUR" &&
            r.Status == "Active" &&
            r.AccountId != Guid.Empty);
    }

    [Fact]
    public async Task Given_ValidCommand_When_Handle_Then_PersistsAccountToRepository()
    {
        var command = new CreateAccountCommand("owner-123", "USD", 0m);
        Account? capturedAccount = null;

        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => capturedAccount = a)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.HandleAsync(command, CancellationToken.None);

        _accountRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedAccount.Should().NotBeNull();
        capturedAccount!.OwnerName.Should().Be("owner-123");
        capturedAccount.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public async Task Given_InvalidCurrency_When_Handle_Then_ReturnsValidationError()
    {
        var command = new CreateAccountCommand("owner-123", "XYZ", 100m);

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeFailureWithCode("Account.InvalidCurrency");

        _accountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository must not be called when currency is invalid");
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("eur")]
    [InlineData("usd")]
    public async Task Given_SupportedCurrencies_When_Handle_Then_Succeeds(string currency)
    {
        var command = new CreateAccountCommand("owner-1", currency, 0m);
        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.HandleAsync(command, CancellationToken.None);

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Given_RepositoryThrows_When_Handle_Then_ExceptionPropagates()
    {
        var command = new CreateAccountCommand("owner-1", "EUR", 100m);
        _accountRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var act = () => _sut.HandleAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB connection lost");
    }
}
