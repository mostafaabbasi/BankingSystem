using BankingSystem.Application.Accounts.GetAccount;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.UnitTests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankingSystem.UnitTests.Application.Accounts.GetAccount;

public sealed class GetAccountHandlerTests
{
    private readonly Mock<IAccountRepository> _repositoryMock = new();
    private readonly GetAccountHandler _sut;

    public GetAccountHandlerTests()
    {
        _sut = new GetAccountHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Given_ExistingAccount_When_Handle_Then_ReturnsAccountResponse()
    {
        var account = TestDataFactory.CreateActiveAccountWithOwner("owner-xyz", balance: 750m);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Account>.Success(account));

        var result = await _sut.HandleAsync(new GetAccountQuery(account.Id), CancellationToken.None);

        result.Should().BeSuccess();
        result.Value.Should().Match<AccountResponse>(r =>
            r.AccountId == account.Id &&
            r.OwnerName == "owner-xyz" &&
            r.Balance == 750m &&
            r.Status == "Active");
    }

    [Fact]
    public async Task Given_NonExistentAccount_When_Handle_Then_ReturnsNotFoundError()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(nameof(Account), id));

        var result = await _sut.HandleAsync(new GetAccountQuery(id), CancellationToken.None);

        result.Should().BeFailureWithCode("Account.NotFound");
    }

    [Fact]
    public async Task Given_Query_When_Handle_Then_QueriesCorrectId()
    {
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound(nameof(Account), id));

        await _sut.HandleAsync(new GetAccountQuery(id), CancellationToken.None);

        _repositoryMock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
