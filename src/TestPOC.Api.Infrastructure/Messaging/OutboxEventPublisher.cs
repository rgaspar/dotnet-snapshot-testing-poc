using System.Text.Json;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.Api.Infrastructure.Persistence.Entities;

namespace TestPOC.Api.Infrastructure.Messaging;

/// <summary>
/// IEventPublisher implementation that persists the event into the outbox table
/// as part of the caller's DbContext unit-of-work. The actual RabbitMQ publish
/// is done by <see cref="OutboxProcessor"/> in the background, so the caller's
/// DB write + event capture happen atomically (single SaveChanges).
/// </summary>
public sealed class OutboxEventPublisher(AppDbContext dbContext, TimeProvider timeProvider) : IEventPublisher
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

	private readonly AppDbContext _dbContext = dbContext;
	private readonly TimeProvider _timeProvider = timeProvider;

	public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
		where TEvent : class
	{
		var message = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			Type = typeof(TEvent).Name,
			Payload = JsonSerializer.Serialize(@event, SerializerOptions),
			CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
			SentAt = null,
			Attempts = 0,
		};

		_dbContext.OutboxMessages.Add(message);
		return Task.CompletedTask;
	}
}
