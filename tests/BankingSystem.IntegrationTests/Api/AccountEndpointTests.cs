using System.Net;
using System.Net.Http.Json;
using BankingSystem.Application.Accounts.CreateAccount;
using BankingSystem.Application.Accounts.GetAccount;
using BankingSystem.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace BankingSystem.IntegrationTests.Api;

public sealed class AccountEndpointTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{

    [Fact]
    public async Task POST_Accounts_Given_ValidRequest_Then_Returns201WithAccountDetails()
    {
        var command = new CreateAccountCommand("owner-integration-1", "EUR", 500m);

        var response = await Client.PostAsJsonAsync("/api/accounts", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateAccountResponse>();
        body.Should().NotBeNull();
        body!.AccountId.Should().NotBeEmpty();
        body.OwnerName.Should().Be("owner-integration-1");
        body.Balance.Should().Be(500m);
        body.Currency.Should().Be("EUR");
        body.Status.Should().Be("Active");
    }

    [Fact]
    public async Task POST_Accounts_Given_ValidRequest_Then_LocationHeaderPointsToNewAccount()
    {
        var command = new CreateAccountCommand("owner-loc-test", "USD", 0m);

        var response = await Client.PostAsJsonAsync("/api/accounts", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/api/accounts/");
    }

    [Fact]
    public async Task POST_Accounts_Given_EmptyOwnerName_Then_Returns400()
    {
        var command = new CreateAccountCommand("", "EUR", 100m);

        var response = await Client.PostAsJsonAsync("/api/accounts", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Accounts_Given_UnsupportedCurrency_Then_Returns400()
    {
        var command = new CreateAccountCommand("owner-1", "BTC", 100m);

        var response = await Client.PostAsJsonAsync("/api/accounts", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Accounts_Given_NegativeBalance_Then_Returns400()
    {
        var command = new CreateAccountCommand("owner-1", "EUR", -1m);

        var response = await Client.PostAsJsonAsync("/api/accounts", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task GET_Account_Given_ExistingId_Then_Returns200WithAccountDetails()
    {
        var created = await CreateAccountAsync(currency: "GBP", initialBalance: 250m);

        var response = await Client.GetAsync($"/api/accounts/{created.AccountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.AccountId.Should().Be(created.AccountId);
        body.Balance.Should().Be(250m);
        body.Currency.Should().Be("GBP");
        body.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GET_Account_Given_NonExistentId_Then_Returns404()
    {
        var id = Guid.NewGuid();

        var response = await Client.GetAsync($"/api/accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Account_Given_InvalidGuid_Then_Returns400Or404()
    {
        var response = await Client.GetAsync("/api/accounts/not-a-guid");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task POST_Then_GET_Given_CreatedAccount_Then_DataPersistedCorrectly()
    {
        var command = new CreateAccountCommand("owner-persist-test", "USD", 9999.99m);

        var postResponse = await Client.PostAsJsonAsync("/api/accounts", command);
        var created = await postResponse.Content.ReadFromJsonAsync<CreateAccountResponse>();

        var getResponse = await Client.GetAsync($"/api/accounts/{created!.AccountId}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<AccountResponse>();

        fetched!.OwnerName.Should().Be("owner-persist-test");
        fetched.Balance.Should().Be(9999.99m);
        fetched.Currency.Should().Be("USD");
    }
}
