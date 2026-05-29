using BankingSystem.Application.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace BankingSystem.Infrastructure.Idempotency;

public sealed class RedisIdempotencyService(
    IDistributedCache cache,
    ILogger<RedisIdempotencyService> logger) : IIdempotencyService
{
    private const string Prefix = "idempotency:";

    public async Task<bool> HasBeenProcessedAsync(string key, CancellationToken ct = default)
    {
        var value = await cache.GetStringAsync(BuildKey(key), ct);
        return value is not null;
    }

    public async Task MarkAsProcessedAsync(
        string key,
        string responsePayload,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromDays(7)
        };

        logger.LogDebug("Marking idempotency key as processed: {Key}", key);
        await cache.SetStringAsync(BuildKey(key), responsePayload, options, ct);
    }

    public async Task<string?> GetCachedResponseAsync(string key, CancellationToken ct = default) =>
        await cache.GetStringAsync(BuildKey(key), ct);

    private static string BuildKey(string key) => $"{Prefix}{key}";
}
