namespace BankingSystem.Application.Common.Dispatcher;

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;

    /// <summary>Used by the outbox processor when event type is only known at runtime.</summary>
    Task PublishAsync(object @event, CancellationToken ct = default);
}
