using RabbitMQ.Client;

namespace TestPOC.Api.Infrastructure.Messaging;

/// <summary>
/// Low-level RabbitMQ publisher used by <see cref="OutboxProcessor"/>.
/// Lazy-declares the exchange on first publish, keeps a single IChannel open
/// for the lifetime of the app, publishes to the topic exchange by routing key.
/// </summary>
public sealed class RabbitMqPublisher(IConnection connection) : IAsyncDisposable
{
	public const string ExchangeName = "testpoc.events";

	private readonly IConnection _connection = connection;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private IChannel? _channel;

	public async Task PublishAsync(string routingKey, string type, byte[] payload, CancellationToken cancellationToken)
	{
		var channel = await GetChannelAsync(cancellationToken);
		var properties = new BasicProperties
		{
			ContentType = "application/json",
			ContentEncoding = "utf-8",
			MessageId = Guid.NewGuid().ToString("N"),
			Type = type,
		};

		await channel.BasicPublishAsync(
			exchange: ExchangeName,
			routingKey: routingKey,
			mandatory: false,
			basicProperties: properties,
			body: payload,
			cancellationToken: cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (_channel is not null)
		{
			await _channel.DisposeAsync();
		}
	}

	private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
	{
		if (_channel is { IsOpen: true })
		{
			return _channel;
		}

		await _initLock.WaitAsync(cancellationToken);
		try
		{
			if (_channel is { IsOpen: true })
			{
				return _channel;
			}

			var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
			await channel.ExchangeDeclareAsync(
				exchange: ExchangeName,
				type: ExchangeType.Topic,
				durable: true,
				autoDelete: false,
				cancellationToken: cancellationToken);

			_channel = channel;
			return channel;
		}
		finally
		{
			_initLock.Release();
		}
	}
}
