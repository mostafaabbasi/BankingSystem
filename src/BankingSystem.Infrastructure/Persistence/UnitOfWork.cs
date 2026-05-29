using BankingSystem.Application.Common;
using BankingSystem.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BankingSystem.Infrastructure.Persistence;

public sealed class UnitOfWork(BankingDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("idempotency_key") == true)
        {
            throw new DuplicateKeyException("A record with the same idempotency key already exists.");
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
