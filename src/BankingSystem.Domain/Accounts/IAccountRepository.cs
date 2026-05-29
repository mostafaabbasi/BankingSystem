using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Accounts;

public interface IAccountRepository
{
    Task<Result<Account>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<Account>> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
}
