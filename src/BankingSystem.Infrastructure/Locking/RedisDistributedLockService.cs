using BankingSystem.Application.Common;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BankingSystem.Infrastructure.Locking;

public sealed class RedisDistributedLockService(
    IConnectionMultiplexer redis,
    ILogger<RedisDistributedLockService> logger) : IDistributedLockService
{
    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string resource,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var lockToken = Guid.NewGuid().ToString();
        var lockKey = $"lock:{resource}";

        var acquired = await db.StringSetAsync(
            lockKey,
            lockToken,
            expiry,
            When.NotExists);

        if (!acquired)
        {
            logger.LogWarning("Failed to acquire lock on resource: {Resource}", resource);
            return null;
        }

        logger.LogDebug("Acquired lock on: {Resource}", resource);
        return new RedisLock(db, lockKey, lockToken, logger);
    }

    private sealed class RedisLock(
        IDatabase db,
        string lockKey,
        string lockToken,
        ILogger logger) : IAsyncDisposable
    {
        private const string ReleaseScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        public async ValueTask DisposeAsync()
        {
            var released = await db.ScriptEvaluateAsync(
                ReleaseScript,
                [new RedisKey(lockKey)],
                [new RedisValue(lockToken)]);

            if ((long)released == 1)
                logger.LogDebug("Released lock: {Key}", lockKey);
            else
                logger.LogWarning("Lock already expired or stolen: {Key}", lockKey);
        }
    }
}
