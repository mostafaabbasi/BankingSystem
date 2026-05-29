using BankingSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankingSystem.IntegrationTests.Common;

public sealed class BankingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .WithDatabase("banking_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.3.0-management")
        .WithUsername("banking_test")
        .WithPassword("banking_test")
        .Build();

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _rabbitMq.StartAsync());

        await RunMigrationsAsync();

        Client = CreateClient();
    }

    private async Task RunMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new BankingDbContext(options, NullLogger<BankingDbContext>.Instance);
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BankingDbContext>>();
            services.RemoveAll<BankingDbContext>();

            services.AddDbContext<BankingDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            services.Configure<Microsoft.Extensions.Options.IOptions<object>>(_ => { });
        });

        var rabbitUri = new Uri(_rabbitMq.GetConnectionString());
        var rabbitCredentials = rabbitUri.UserInfo.Split(':');
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("RabbitMq:Host", rabbitUri.Host);
        builder.UseSetting("RabbitMq:Port", rabbitUri.Port.ToString());
        builder.UseSetting("RabbitMq:Username", rabbitCredentials[0]);
        builder.UseSetting("RabbitMq:Password", rabbitCredentials[1]);
    }

    public BankingDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    }

    public async Task ResetDatabaseAsync()
    {
        var db = GetDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE transactions, accounts, outbox_messages RESTART IDENTITY CASCADE");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
