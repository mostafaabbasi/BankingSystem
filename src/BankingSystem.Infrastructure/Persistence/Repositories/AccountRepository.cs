using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BankingSystem.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(BankingDbContext context) : IAccountRepository
{
    public async Task<Result<Account>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return account is null
            ? Error.NotFound(nameof(Account), id)
            : Result<Account>.Success(account);
    }

    public async Task<Result<Account>> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return account is null
            ? Error.NotFound(nameof(Account), id)
            : Result<Account>.Success(account);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await context.Accounts.AnyAsync(a => a.Id == id, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default) =>
        await context.Accounts.AddAsync(account, ct);

    public Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        context.Accounts.Update(account);
        return Task.CompletedTask;
    }
}
