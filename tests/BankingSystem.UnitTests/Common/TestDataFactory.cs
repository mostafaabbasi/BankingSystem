using Bogus;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Transactions;

namespace BankingSystem.UnitTests.Common;

public static class TestDataFactory
{
    private static readonly Faker Faker = new("en");


    public static Account CreateActiveAccount(
        decimal balance = 1000m,
        Currency currency = Currency.EUR)
    {
        var result = Account.Create(
            ownerName: Faker.Internet.UserName(),
            currency: currency,
            initialBalance: balance);

        return result.Value;
    }

    public static Account CreateActiveAccountWithOwner(
        string ownerName,
        decimal balance = 1000m,
        Currency currency = Currency.EUR)
    {
        var result = Account.Create(ownerName, currency, balance);
        return result.Value;
    }


    public static Transaction CreatePendingTransaction(
        Guid? fromAccountId = null,
        Guid? toAccountId = null,
        decimal amount = 100m,
        string currency = "EUR",
        string? idempotencyKey = null)
    {
        var result = Transaction.Create(
            fromAccountId: fromAccountId ?? Guid.NewGuid(),
            toAccountId: toAccountId ?? Guid.NewGuid(),
            amount: amount,
            currency: currency,
            idempotencyKey: idempotencyKey ?? Guid.NewGuid().ToString(),
            correlationId: Guid.NewGuid().ToString());

        return result.Value;
    }


    public static string RandomOwnerName() => Faker.Internet.UserName();
    public static string RandomIdempotencyKey() => Guid.NewGuid().ToString();
    public static decimal RandomPositiveAmount(decimal max = 500m) =>
        Math.Round(Faker.Random.Decimal(1m, max), 2);
}
