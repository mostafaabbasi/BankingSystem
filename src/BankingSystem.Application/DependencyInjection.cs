using BankingSystem.Application.Common.Behaviors;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Application.Transactions.Saga;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BankingSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;

                var def = iface.GetGenericTypeDefinition();
                if (def == typeof(ICommandHandler<,>) || def == typeof(IQueryHandler<,>))
                    services.AddScoped(iface, type);
            }
        }

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<InitiateTransferSagaHandler>();
        services.AddScoped<DebitAccountSagaHandler>();
        services.AddScoped<CreditAccountSagaHandler>();
        services.AddScoped<CompleteTransferSagaHandler>();
        services.AddScoped<RollbackDebitSagaHandler>();

        return services;
    }
}
