namespace BankingSystem.Application.Common;

public interface IDistributedLockService
{
    Task<IAsyncDisposable?> AcquireLockAsync(
        string resource,
        TimeSpan expiry,
        CancellationToken ct = default);
}
