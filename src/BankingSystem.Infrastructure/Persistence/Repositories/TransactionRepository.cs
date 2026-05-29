using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace BankingSystem.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(BankingDbContext context) : ITransactionRepository
{
    public async Task<Result<Transaction>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tx = await context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return tx is null
            ? Error.NotFound(nameof(Transaction), id)
            : Result<Transaction>.Success(tx);
    }

    public async Task<Result<Transaction>> GetByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken ct = default)
    {
        var tx = await context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

        return tx is null
            ? Error.NotFound(nameof(Transaction), Guid.Empty)
            : Result<Transaction>.Success(tx);
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        await context.Transactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(
        Guid accountId, int page, int pageSize, CancellationToken ct = default)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default) =>
        await context.Transactions.AddAsync(transaction, ct);

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        context.Transactions.Update(transaction);
        return Task.CompletedTask;
    }
}
