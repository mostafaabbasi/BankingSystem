using System.Reflection;
using BankingSystem.Api.Common;
using BankingSystem.Application;
using BankingSystem.Infrastructure;
using Elastic.Serilog.Sinks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        var elasticsearchUrl = ctx.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200";

        cfg.ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "BankingSystem")
            .WriteTo.Console()
            .WriteTo.Elasticsearch([new Uri(elasticsearchUrl)]);
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("BankingSystem"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("MassTransit")
            .AddZipkinExporter(o =>
                o.Endpoint = new Uri(
                    builder.Configuration["Zipkin:Endpoint"] ?? "http://localhost:9411/api/v2/spans")))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddPrometheusExporter());

    builder.Services.AddOpenApi();

    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization();
    builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
        .AddRabbitMQ(sp =>
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(
                    $"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}/")
            };
            return factory.CreateConnectionAsync();
        });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        await app.Services.MigrateDatabaseAsync();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapEndpoints();
    app.MapHealthChecks("/health");
    app.MapPrometheusScrapingEndpoint("/metrics");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;