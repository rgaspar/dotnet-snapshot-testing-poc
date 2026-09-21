using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestPOC.Api.Infrastructure.Persistence;

namespace TestPOC.Api.Infrastructure.Messaging;

/// <summary>
/// Background worker that polls the outbox table for pending messages,
/// publishes them to RabbitMQ, and marks them as sent. At-least-once delivery:
/// a crash between publish and mark-as-sent will re-publish on the next poll
/// (consumers must be idempotent).
/// </summary>
public sealed class OutboxProcessor(
	IServiceScopeFactory scopeFactory,
	RabbitMqPublisher rabbitMqPublisher,
	TimeProvider timeProvider,
	ILogger<OutboxProcessor> logger) : BackgroundService
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
	private const int BatchSize = 32;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessBatchAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Outbox batch failed");
			}

			try
			{
				await Task.Delay(PollInterval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}

	private async Task ProcessBatchAsync(CancellationToken cancellationToken)
	{
		using var scope = scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		var pending = await dbContext.OutboxMessages
			.Where(message => message.SentAt == null)
			.OrderBy(message => message.CreatedAt)
			.Take(BatchSize)
			.ToListAsync(cancellationToken);

		if (pending.Count == 0)
		{
			return;
		}

		foreach (var message in pending)
		{
			message.Attempts++;
			await rabbitMqPublisher.PublishAsync(
				routingKey: message.Type,
				type: message.Type,
				payload: Encoding.UTF8.GetBytes(message.Payload),
				cancellationToken: cancellationToken);
			message.SentAt = timeProvider.GetUtcNow().UtcDateTime;
		}

		await dbContext.SaveChangesAsync(cancellationToken);
	}
}
