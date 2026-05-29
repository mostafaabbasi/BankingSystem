using BankingSystem.Application.Common;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using BankingSystem.Infrastructure.Idempotency;
using BankingSystem.Infrastructure.Locking;
using BankingSystem.Infrastructure.Messaging;
using BankingSystem.Infrastructure.Messaging.Consumers;
using BankingSystem.Infrastructure.Outbox;
using BankingSystem.Infrastructure.Persistence;
using BankingSystem.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BankingSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BankingDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(BankingDbContext).Assembly.FullName);
                        npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    })
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var redisConnectionString = configuration.GetConnectionString("Redis")!;
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddStackExchangeRedisCache(options =>
            options.Configuration = redisConnectionString);

        services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<InitiateTransferConsumer>();
            bus.AddConsumer<DebitAccountConsumer>();
            bus.AddConsumer<CreditAccountConsumer>();
            bus.AddConsumer<CompleteTransferConsumer>();
            bus.AddConsumer<RollbackDebitConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                var port = ushort.TryParse(configuration["RabbitMq:Port"], out var p) ? p : (ushort)5672;
                cfg.Host(configuration["RabbitMq:Host"], port, configuration["RabbitMq:VHost"], h =>
                {
                    h.Username(configuration["RabbitMq:Username"]!);
                    h.Password(configuration["RabbitMq:Password"]!);
                });

                cfg.UseMessageRetry(r => r.Exponential(
                    5,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(5)));

                cfg.ReceiveEndpoint("transfer.initiate", e =>
                {
                    e.ConfigureConsumer<InitiateTransferConsumer>(ctx);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });

                cfg.ReceiveEndpoint("transfer.debit", e =>
                {
                    e.ConfigureConsumer<DebitAccountConsumer>(ctx);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });

                cfg.ReceiveEndpoint("transfer.credit", e =>
                {
                    e.ConfigureConsumer<CreditAccountConsumer>(ctx);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });

                cfg.ReceiveEndpoint("transfer.complete", e =>
                {
                    e.ConfigureConsumer<CompleteTransferConsumer>(ctx);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });

                cfg.ReceiveEndpoint("transfer.rollback", e =>
                {
                    e.ConfigureConsumer<RollbackDebitConsumer>(ctx);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddScoped<IMessageBus, RabbitMqMessageBus>();

        var assembly = typeof(DependencyInjection).Assembly;
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                    services.AddScoped(iface, type);
            }
        }

        services.AddHostedService<OutboxProcessorJob>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        await db.Database.MigrateAsync();
    }
}