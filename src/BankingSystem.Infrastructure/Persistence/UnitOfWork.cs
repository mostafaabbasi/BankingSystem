using BankingSystem.Domain.Common;

namespace BankingSystem.Infrastructure.Persistence;

public sealed class UnitOfWork(BankingDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);

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
