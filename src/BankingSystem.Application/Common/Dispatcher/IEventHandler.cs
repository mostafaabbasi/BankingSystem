namespace BankingSystem.Application.Common.Dispatcher;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
