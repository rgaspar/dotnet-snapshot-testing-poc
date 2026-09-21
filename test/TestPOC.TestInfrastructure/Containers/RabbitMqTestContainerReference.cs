using Testcontainers.RabbitMq;

namespace TestPOC.TestInfrastructure.Containers;

/// <summary>
/// Owns the singleton RabbitMQ Testcontainer used by every integration test that
/// exercises outbound event publishing. Mirrors <see cref="SharedPostgresContainerReference"/>:
/// <c>WithReuse(true)</c> on every run, no in-process dispose, Ryuk cleans up
/// when reuse is not honored by the host.
///
/// See <see cref="SharedPostgresContainerReference"/> for the host-side opt-in required
/// for container reuse (<c>~/.testcontainers.properties</c> with
/// <c>testcontainers.reuse.enable=true</c>).
/// </summary>
public sealed class RabbitMqTestContainerReference : IAsyncDisposable
{
	public static readonly RabbitMqTestContainerReference Instance = new();

	private const ushort HostPort = 32773;
	private const ushort ManagementHostPort = 15672;

	private readonly LazyAsyncStartup<RabbitMqTestContainerReference> _initializer;
	private RabbitMqContainer? _container;
	private string? _connectionString;

	public RabbitMqTestContainerReference()
	{
		_initializer = new LazyAsyncStartup<RabbitMqTestContainerReference>(
			static (self, cancel) => self.StartContainerAsync(cancel));
	}

	public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
	{
		return _initializer.EnsureInitialized(this, cancellationToken);
	}

	public string GetConnectionString()
	{
		return _connectionString
			?? throw new InvalidOperationException("Container is not initialized. Call EnsureInitializedAsync first.");
	}

	public ValueTask DisposeAsync()
	{
		// Reuse handled by Testcontainers via WithReuse(true) + host opt-in.
		// Ryuk takes over cleanup when reuse is not honored.
		return ValueTask.CompletedTask;
	}

	private async Task StartContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RabbitMqBuilder()
			.WithImage("rabbitmq:3.13-management-alpine")
			.WithUsername("guest")
			.WithPassword("guest")
			.WithPortBinding(HostPort, 5672)
			.WithPortBinding(ManagementHostPort, 15672)
			.WithLabel("com.docker.compose.project", "testpoc")
			.WithLabel("com.docker.compose.service", "rabbitmq")
			.WithReuse(true)
			.Build();

		await _container.StartAsync(cancellationToken);
		_connectionString = _container.GetConnectionString();
	}
}
