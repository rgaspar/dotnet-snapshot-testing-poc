namespace TestPOC.Api.Infrastructure.Messaging;

public interface IEventPublisher
{
	Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
		where TEvent : class;
}
