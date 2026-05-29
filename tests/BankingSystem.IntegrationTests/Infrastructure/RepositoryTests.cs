using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Transactions;
using BankingSystem.Infrastructure.Persistence;
using BankingSystem.Infrastructure.Persistence.Repositories;
using BankingSystem.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BankingSystem.IntegrationTests.Infrastructure;

public sealed class AccountRepositoryTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{
    private BankingDbContext GetDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<BankingDbContext>();

    [Fact]
    public async Task AddAsync_Given_NewAccount_Then_PersistedToDatabase()
    {
        var db = GetDb();
        var repo = new AccountRepository(db);
        var account = Account.Create("owner-db-1", Currency.EUR, 750m).Value;

        await repo.AddAsync(account);
        await db.SaveChangesAsync();

        var saved = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == account.Id);
        saved.Should().NotBeNull();
        saved!.OwnerName.Should().Be("owner-db-1");
        saved.Balance.Should().Be(750m);
        saved.Currency.Should().Be(Currency.EUR);
        saved.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task GetByIdAsync_Given_ExistingAccount_Then_ReturnsAccount()
    {
        var db = GetDb();
        var repo = new AccountRepository(db);
        var account = Account.Create("owner-db-2", Currency.USD, 100m).Value;
        await repo.AddAsync(account);
        await db.SaveChangesAsync();

        var result = await repo.GetByIdAsync(account.Id);

        result.Should().BeSuccess()
            .Which.OwnerName.Should().Be("owner-db-2");
    }

    [Fact]
    public async Task GetByIdAsync_Given_NonExistentId_Then_ReturnsNotFoundError()
    {
        var db = GetDb();
        var repo = new AccountRepository(db);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        result.Should().BeFailureWithCode("Account.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_Given_ModifiedAccount_Then_PersistsChanges()
    {
        var db = GetDb();
        var repo = new AccountRepository(db);
        var account = Account.Create("owner-update", Currency.EUR, 500m).Value;
        await repo.AddAsync(account);
        await db.SaveChangesAsync();

        account.Debit(200m, Guid.NewGuid());
        await repo.UpdateAsync(account);
        await db.SaveChangesAsync();

        var fresh = await new AccountRepository(GetDb()).GetByIdAsync(account.Id);
        fresh.Value.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task OptimisticConcurrency_Given_ConcurrentUpdate_Then_ThrowsDbUpdateConcurrencyException()
    {
        var db1 = GetDb();
        var db2 = GetDb();

        var account = Account.Create("owner-concurrency", Currency.EUR, 1000m).Value;
        await new AccountRepository(db1).AddAsync(account);
        await db1.SaveChangesAsync();

        var r1 = await new AccountRepository(db1).GetByIdForUpdateAsync(account.Id);
        var r2 = await new AccountRepository(db2).GetByIdForUpdateAsync(account.Id);

        r1.Value.Debit(100m, Guid.NewGuid());
        await new AccountRepository(db1).UpdateAsync(r1.Value);
        await db1.SaveChangesAsync();

        r2.Value.Debit(100m, Guid.NewGuid());
        await new AccountRepository(db2).UpdateAsync(r2.Value);

        var act = () => db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the second writer has a stale RowVersion and must be rejected");
    }
}

public sealed class TransactionRepositoryTests(BankingApiFactory factory)
    : IntegrationTestBase(factory)
{
    private BankingDbContext GetDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<BankingDbContext>();

    [Fact]
    public async Task AddAsync_Given_NewTransaction_Then_PersistedToDatabase()
    {
        var db = GetDb();
        var repo = new TransactionRepository(db);
        var tx = Transaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), 250m, "EUR", Guid.NewGuid().ToString()).Value;

        await repo.AddAsync(tx);
        await db.SaveChangesAsync();

        var saved = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tx.Id);
        saved.Should().NotBeNull();
        saved!.Amount.Should().Be(250m);
        saved.Status.Should().Be(TransactionStatus.Pending);
        saved.IdempotencyKey.Should().Be(tx.IdempotencyKey);
    }

    [Fact]
    public async Task ExistsByIdempotencyKeyAsync_Given_ExistingKey_Then_ReturnsTrue()
    {
        var db = GetDb();
        var repo = new TransactionRepository(db);
        var key = Guid.NewGuid().ToString();
        var tx = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", key).Value;
        await repo.AddAsync(tx);
        await db.SaveChangesAsync();

        var exists = await repo.ExistsByIdempotencyKeyAsync(key);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByIdempotencyKeyAsync_Given_UnknownKey_Then_ReturnsFalse()
    {
        var db = GetDb();
        var repo = new TransactionRepository(db);

        var exists = await repo.ExistsByIdempotencyKeyAsync(Guid.NewGuid().ToString());

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateIdempotencyKey_Given_SameKey_When_AddBoth_Then_ThrowsUniqueConstraintViolation()
    {
        var db = GetDb();
        var repo = new TransactionRepository(db);
        var key = "unique-idem-key-test";

        var tx1 = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", key).Value;
        var tx2 = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 200m, "EUR", key).Value;

        await repo.AddAsync(tx1);
        await db.SaveChangesAsync();

        await repo.AddAsync(tx2);

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "duplicate idempotency keys must be rejected by the database");
    }

    [Fact]
    public async Task GetByAccountIdAsync_Given_TransactionsForAccount_Then_ReturnsPaged()
    {
        // Arrange
        var db = GetDb();
        var repo = new TransactionRepository(db);
        var accountId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            var tx = Transaction.Create(accountId, Guid.NewGuid(), 10m * (i + 1), "EUR",
                Guid.NewGuid().ToString()).Value;
            await repo.AddAsync(tx);
        }

        await db.SaveChangesAsync();

        var page1 = await repo.GetByAccountIdAsync(accountId, page: 1, pageSize: 3);
        var page2 = await repo.GetByAccountIdAsync(accountId, page: 2, pageSize: 3);

        page1.Should().HaveCount(3);
        page2.Should().HaveCount(2);
    }
}