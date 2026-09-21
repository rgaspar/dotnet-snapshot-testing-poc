using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TestPOC.Api.Dtos;
using TestPOC.Api.Infrastructure.Messaging;
using TestPOC.Api.Services;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.Rabbit;

/// <summary>
/// Snapshot-tests the outbound RabbitMQ event that <see cref="ItemsService.CreateAsync"/>
/// publishes after a successful item creation. Same JsonTestHelpers + SnapshotHelper
/// pair as the REST/GraphQL tests, so one snapshot pattern covers all three transports.
/// </summary>
public sealed class EventPublishTests : IClassFixture<ApiTestFactory>, IAsyncLifetime
{
	private readonly ApiTestFactory _factory;
	private readonly HttpClient _client;

	public EventPublishTests(ApiTestFactory factory)
	{
		_factory = factory;
		_client = factory.CreateAuthenticatedClient();
	}

	public Task InitializeAsync() => _factory.ResetDataToSeedAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Should_PublishItemCreatedEvent_When_ItemCreatedViaRest()
	{
		var subscription = await SubscribeOneMessageAsync(nameof(TestPOC.Api.Infrastructure.Messaging.Events.ItemCreatedEvent));

		var input = new CreateItemRequest { Name = ValidItem.NewItemName, Price = ValidItem.Price };
		var response = await _client.PostAsJsonAsync("/api/items", input);
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		var payload = await subscription.WaitAsync(TimeSpan.FromSeconds(5));
		var stripped = JsonTestHelpers.StripFields(payload, "id", "createdAt");

		SnapshotHelper.ValidateJson(stripped);
	}

	private async Task<Task<string>> SubscribeOneMessageAsync(string routingKey)
	{
		var factory = new ConnectionFactory { Uri = new Uri(_factory.RabbitMqConnectionString) };
		var connection = await factory.CreateConnectionAsync();
		var channel = await connection.CreateChannelAsync();

		await channel.ExchangeDeclareAsync(
			exchange: RabbitMqPublisher.ExchangeName,
			type: ExchangeType.Topic,
			durable: true,
			autoDelete: false);

		var queue = await channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
		await channel.QueueBindAsync(queue: queue.QueueName, exchange: RabbitMqPublisher.ExchangeName, routingKey: routingKey);

		var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.ReceivedAsync += async (_, delivery) =>
		{
			completion.TrySetResult(Encoding.UTF8.GetString(delivery.Body.Span));
			await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
		};

		await channel.BasicConsumeAsync(queue: queue.QueueName, autoAck: false, consumer: consumer);

		_ = completion.Task.ContinueWith(async _ =>
		{
			await channel.DisposeAsync();
			await connection.DisposeAsync();
		}, TaskScheduler.Default);

		return completion.Task;
	}
}
