using Microsoft.Extensions.DependencyInjection;

namespace BankingSystem.Application.Common.Dispatcher;

public sealed class Dispatcher(IServiceProvider sp) : IDispatcher
{
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
        => ExecuteWithPipelineAsync<TResponse>(command, typeof(ICommandHandler<,>), ct);

    public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
        => ExecuteWithPipelineAsync<TResponse>(query, typeof(IQueryHandler<,>), ct);

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
        => PublishCoreAsync(@event, typeof(TEvent), ct);

    public Task PublishAsync(object @event, CancellationToken ct = default)
        => PublishCoreAsync(@event, @event.GetType(), ct);

    private async Task<TResponse> ExecuteWithPipelineAsync<TResponse>(
        object request, Type handlerOpenType, CancellationToken ct)
    {
        var requestType = request.GetType();
        var handlerType = handlerOpenType.MakeGenericType(requestType, typeof(TResponse));
        var handler = sp.GetRequiredService(handlerType);

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = sp.GetServices(behaviorType).ToList();

        Func<CancellationToken, Task<TResponse>> pipeline =
            ct2 => InvokeHandlerAsync<TResponse>(handler, request, ct2);

        foreach (var behavior in Enumerable.Reverse(behaviors))
        {
            var captured = behavior!;
            var next = pipeline;
            pipeline = ct2 => InvokeBehaviorAsync<TResponse>(captured, request, next, ct2);
        }

        return await pipeline(ct);
    }

    private async Task PublishCoreAsync(object @event, Type eventType, CancellationToken ct)
    {
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = sp.GetServices(handlerType).ToList();

        foreach (var handler in handlers)
        {
            var method = handler!.GetType().GetMethod(nameof(IEventHandler<object>.HandleAsync))!;
            await (Task)method.Invoke(handler, [@event, ct])!;
        }
    }

    private static Task<TResponse> InvokeHandlerAsync<TResponse>(
        object handler, object request, CancellationToken ct)
    {
        var method = handler.GetType().GetMethod("HandleAsync")!;
        return (Task<TResponse>)method.Invoke(handler, [request, ct])!;
    }

    private static Task<TResponse> InvokeBehaviorAsync<TResponse>(
        object behavior, object request,
        Func<CancellationToken, Task<TResponse>> next,
        CancellationToken ct)
    {
        var method = behavior.GetType().GetMethod("HandleAsync")!;
        return (Task<TResponse>)method.Invoke(behavior, [request, next, ct])!;
    }
}
