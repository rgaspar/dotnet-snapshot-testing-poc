using Testcontainers.PostgreSql;

namespace TestPOC.TestInfrastructure.Containers;

/// <summary>
/// Owns the singleton PostgreSQL Testcontainer used by every integration test.
///
/// **Container reuse (`WithReuse(true)`):**
/// Every run — <c>dotnet test</c>, <c>dotnet run</c>, VS/Rider test explorer,
/// debugger attached or not — asks Testcontainers for a reused container matched
/// by name/labels (<c>testpoc-postgres</c>). When reuse is honored (see host
/// opt-in below), the second run onward starts in &lt;1s. When reuse is ignored,
/// Testcontainers falls back to Ryuk cleanup, so no zombie containers linger.
///
/// **Requires host-side opt-in** — Testcontainers only reuses containers if the
/// dev has enabled it in <c>~/.testcontainers.properties</c>:
///
///   testcontainers.reuse.enable=true
///
/// On Windows: <c>%USERPROFILE%\.testcontainers.properties</c>. Run once:
///   <c>Set-Content -Path "$env:USERPROFILE\.testcontainers.properties" -Value "testcontainers.reuse.enable=true"</c>
///
/// Without the opt-in, the container is cleaned up at end of run (Ryuk), so the
/// next run pays the 5-10s startup cost again.
/// </summary>
public sealed class SharedPostgresContainerReference : IAsyncDisposable
{
	public static readonly SharedPostgresContainerReference Instance = new();

	private readonly LazyAsyncStartup<SharedPostgresContainerReference> _initializer;
	private PostgreSqlContainer? _container;
	private string? _connectionString;

	public SharedPostgresContainerReference()
	{
		_initializer = new LazyAsyncStartup<SharedPostgresContainerReference>(
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
		// Deliberately do NOT dispose the container from the process: reuse is
		// requested via WithReuse(true), so when the host has opted in
		// (~/.testcontainers.properties) the next run reuses this container.
		// When reuse is ignored, Ryuk handles cleanup automatically.
		return ValueTask.CompletedTask;
	}

	// Fixed host port for the container's 5432. Keeps the connection string stable
	// across runs so psql/pgAdmin/appsettings.json can point at localhost:32772
	// without hunting for a random port after each restart. If this port is busy
	// on your machine, change it and update src/TestPOC.Api/appsettings.json.
	private const ushort HostPort = 32772;

	private async Task StartContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithDatabase("testpoc")
			.WithUsername("testuser")
			.WithPassword("testpass")
			.WithPortBinding(HostPort, 5432)
			.WithLabel("com.docker.compose.project", "testpoc")
			.WithLabel("com.docker.compose.service", "postgres")
			.WithReuse(true)
			.Build();

		await _container.StartAsync(cancellationToken);
		_connectionString = _container.GetConnectionString();
	}
}
