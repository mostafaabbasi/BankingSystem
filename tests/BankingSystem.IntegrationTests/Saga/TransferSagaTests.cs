using BankingSystem.Application.Transactions.GetTransaction;
using BankingSystem.Domain.Transactions;
using BankingSystem.IntegrationTests.Common;
using FluentAssertions;
using System.Net.Http.Json;
using Xunit;

namespace BankingSystem.IntegrationTests.Saga;

public sealed class TransferSagaTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{
    private const int MaxPollAttempts = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Transfer_HappyPath_When_SufficientFunds_Then_TransactionEventuallyCompletes()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);

        var transfer = await TransferAsync(from.AccountId, to.AccountId, 400m);

        var finalStatus = await PollTransactionUntilAsync(
            transfer.TransactionId,
            status => status is "Completed" or "Failed" or "Compensated");

        finalStatus.Should().Be("Completed",
            "transfer should complete when source has sufficient funds");
    }

    [Fact]
    public async Task Transfer_HappyPath_When_Completed_Then_BalancesAreUpdated()
    {
        var from = await CreateAccountAsync(initialBalance: 800m);
        var to = await CreateAccountAsync(initialBalance: 200m);

        var transfer = await TransferAsync(from.AccountId, to.AccountId, 300m);

        await PollTransactionUntilAsync(
            transfer.TransactionId,
            status => status == "Completed");

        var fromBalance = await GetAccountBalanceAsync(from.AccountId);
        var toBalance = await GetAccountBalanceAsync(to.AccountId);

        fromBalance.Should().Be(500m, "source debited by 300");
        toBalance.Should().Be(500m, "destination credited by 300");
    }

    [Fact]
    public async Task Transfer_Idempotency_When_SameKeySubmittedTwice_Then_OnlyOneTransactionCreated()
    {
        var from = await CreateAccountAsync(initialBalance: 1000m);
        var to = await CreateAccountAsync(initialBalance: 0m);
        var key = Guid.NewGuid().ToString();

        var results = await Task.WhenAll(
            TransferAsync(from.AccountId, to.AccountId, 100m, key),
            TransferAsync(from.AccountId, to.AccountId, 100m, key));
        var r1 = results[0];
        var r2 = results[1];

        r1.TransactionId.Should().Be(r2.TransactionId);

        await PollTransactionUntilAsync(r1.TransactionId, s => s == "Completed");

        var fromBalance = await GetAccountBalanceAsync(from.AccountId);
        fromBalance.Should().Be(900m, "only one debit of 100 should have occurred");
    }

    [Fact]
    public async Task Transfer_Concurrency_When_MultipleTransfersFromSameAccount_Then_NoDoubleSpend()
    {
        var from = await CreateAccountAsync(initialBalance: 100m);
        var to1 = await CreateAccountAsync(initialBalance: 0m);
        var to2 = await CreateAccountAsync(initialBalance: 0m);

        var results = await Task.WhenAll(
            SendTransferRawAsync(from.AccountId, to1.AccountId, 75m),
            SendTransferRawAsync(from.AccountId, to2.AccountId, 75m));

        var accepted = results.Where(r => r is System.Net.HttpStatusCode.Accepted).ToList();

        await Task.Delay(TimeSpan.FromSeconds(3));

        var fromBalance = await GetAccountBalanceAsync(from.AccountId);

        fromBalance.Should().BeGreaterThanOrEqualTo(0m,
            "double-spend protection must prevent negative balance");
        fromBalance.Should().BeLessThanOrEqualTo(100m);
    }

    private async Task<string> PollTransactionUntilAsync(
        Guid transactionId,
        Func<string, bool> predicate)
    {
        for (int i = 0; i < MaxPollAttempts; i++)
        {
            var response = await Client.GetAsync($"/api/transactions/{transactionId}");
            if (response.IsSuccessStatusCode)
            {
                var tx = await response.Content.ReadFromJsonAsync<TransactionResponse>();
                if (tx is not null && predicate(tx.Status))
                    return tx.Status;
            }

            await Task.Delay(PollInterval);
        }

        return "Timeout";
    }

    private async Task<decimal> GetAccountBalanceAsync(Guid accountId)
    {
        var response = await Client.GetFromJsonAsync<BankingSystem.Application.Accounts.GetAccount.AccountResponse>(
            $"/api/accounts/{accountId}");
        return response!.Balance;
    }

    private async Task<System.Net.HttpStatusCode> SendTransferRawAsync(
        Guid fromId, Guid toId, decimal amount)
    {
        var key = Guid.NewGuid().ToString();
        var command = new BankingSystem.Application.Transactions.Transfer.TransferCommand(
            fromId, toId, amount, "EUR", key);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/transfer")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", key);

        var response = await Client.SendAsync(request);
        return response.StatusCode;
    }
}
