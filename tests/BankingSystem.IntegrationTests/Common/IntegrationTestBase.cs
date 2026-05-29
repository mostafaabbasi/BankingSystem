using System.Net.Http.Json;
using BankingSystem.Application.Accounts.CreateAccount;
using BankingSystem.Application.Transactions.Transfer;
using Bogus;
using Xunit;

namespace BankingSystem.IntegrationTests.Common;

[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<BankingApiFactory>;

[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase(BankingApiFactory factory) : IAsyncLifetime
{
    protected static readonly Faker Faker = new("en");
    protected HttpClient Client => factory.Client;

    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<CreateAccountResponse> CreateAccountAsync(
        string currency = "EUR",
        decimal initialBalance = 1000m)
    {
        var command = new CreateAccountCommand(Faker.Internet.UserName(), currency, initialBalance);
        var response = await Client.PostAsJsonAsync("/api/accounts", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>())!;
    }

    protected async Task<TransferResponse> TransferAsync(
        Guid fromId,
        Guid toId,
        decimal amount,
        string? idempotencyKey = null)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString();
        var command = new TransferCommand(fromId, toId, amount, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TransferResponse>())!;
    }
}
