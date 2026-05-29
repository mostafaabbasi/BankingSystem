using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Transactions;

public interface ITransactionRepository
{
    Task<Result<Transaction>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<Transaction>> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
}
