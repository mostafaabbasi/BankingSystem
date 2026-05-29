using System.Net;
using System.Net.Http.Json;
using BankingSystem.Application.Transactions.GetTransaction;
using BankingSystem.Application.Transactions.Transfer;
using BankingSystem.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace BankingSystem.IntegrationTests.Api;

public sealed class TransactionEndpointTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{

    [Fact]
    public async Task POST_Transfer_Given_ValidRequest_Then_Returns202WithPendingStatus()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);

        var response = await TransferAsync(from.AccountId, to.AccountId, 250m);

        response.TransactionId.Should().NotBeEmpty();
        response.Status.Should().Be("Pending");
        response.CorrelationId.Should().NotBeNullOrWhiteSpace();
        response.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task POST_Transfer_Given_ValidRequest_Then_HttpResponseIs202Accepted()
    {
        var from = await CreateAccountAsync(initialBalance: 500m);
        var to = await CreateAccountAsync(initialBalance: 0m);
        var key = Guid.NewGuid().ToString();
        var command = new TransferCommand(from.AccountId, to.AccountId, 100m, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task POST_Transfer_Given_InsufficientFunds_Then_Returns400()
    {
        var from = await CreateAccountAsync(initialBalance: 50m);
        var to = await CreateAccountAsync(initialBalance: 0m);
        var key = Guid.NewGuid().ToString();
        var command = new TransferCommand(from.AccountId, to.AccountId, 500m, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Transfer_Given_SameSourceAndDestination_Then_Returns400()
    {
        var account = await CreateAccountAsync(initialBalance: 500m);
        var key = Guid.NewGuid().ToString();
        var command = new TransferCommand(account.AccountId, account.AccountId, 100m, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Transfer_Given_NonExistentSourceAccount_Then_Returns404()
    {
        var to = await CreateAccountAsync();
        var key = Guid.NewGuid().ToString();
        var command = new TransferCommand(Guid.NewGuid(), to.AccountId, 100m, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task POST_Transfer_Given_DuplicateIdempotencyKey_Then_ReturnsSameTransaction()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);
        var key = Guid.NewGuid().ToString();

        var first = await TransferAsync(from.AccountId, to.AccountId, 100m, key);
        var second = await TransferAsync(from.AccountId, to.AccountId, 100m, key);

        second.TransactionId.Should().Be(first.TransactionId,
            "idempotent retries must return the original transaction");
    }

    [Fact]
    public async Task POST_Transfer_Given_DifferentIdempotencyKeys_Then_CreatesSeparateTransactions()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);

        var first = await TransferAsync(from.AccountId, to.AccountId, 50m, Guid.NewGuid().ToString());
        var second = await TransferAsync(from.AccountId, to.AccountId, 50m, Guid.NewGuid().ToString());

        second.TransactionId.Should().NotBe(first.TransactionId);
    }


    [Fact]
    public async Task GET_Transaction_Given_ExistingId_Then_Returns200WithTransactionDetails()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);
        var transfer = await TransferAsync(from.AccountId, to.AccountId, 333m);

        var response = await Client.GetAsync($"/api/transactions/{transfer.TransactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        body.Should().NotBeNull();
        body!.TransactionId.Should().Be(transfer.TransactionId);
        body.FromAccountId.Should().Be(from.AccountId);
        body.ToAccountId.Should().Be(to.AccountId);
        body.Amount.Should().Be(333m);
        body.Currency.Should().Be("EUR");
        body.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GET_Transaction_Given_NonExistentId_Then_Returns404()
    {
        var response = await Client.GetAsync($"/api/transactions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
