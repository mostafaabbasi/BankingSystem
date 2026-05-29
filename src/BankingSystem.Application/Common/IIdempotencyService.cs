namespace BankingSystem.Application.Common;

public interface IIdempotencyService
{
    Task<bool> HasBeenProcessedAsync(string key, CancellationToken ct = default);
    Task MarkAsProcessedAsync(string key, string responsePayload, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<string?> GetCachedResponseAsync(string key, CancellationToken ct = default);
}
