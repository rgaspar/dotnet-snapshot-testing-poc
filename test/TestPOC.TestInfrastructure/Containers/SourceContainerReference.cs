using Bogus;
using Microsoft.EntityFrameworkCore;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.Api.Infrastructure.Persistence.Entities;
using Testcontainers.PostgreSql;

namespace TestPOC.TestInfrastructure.Containers;

/// <summary>
/// Owns the singleton "prod-like source" PostgreSQL Testcontainer used by the
/// currently-skipped SourceCsvRegenerator facts.
/// Started on every test-host boot (parallel to <see cref="SharedPostgresContainerReference"/>)
/// so <c>dotnet test</c> can also exercise the CSV-regeneration workflow
/// end-to-end, not just <c>dotnet run</c>.
///
/// Fixed host port <c>32774</c>, labels matched to the src-side
/// <c>SourceDatabaseInitializer</c>, so reuse hits the same container regardless
/// of whether the API or the test host started it first. Seeds 30 dummy rows if
/// the table is empty; a reused container with rows already there is left alone.
///
/// Same reuse contract as <see cref="SharedPostgresContainerReference"/>:
/// <c>WithReuse(true)</c> on every run, no in-process dispose, Ryuk handles
/// cleanup when the host has not opted in. See <see cref="SharedPostgresContainerReference"/>
/// for the one-liner.
/// </summary>
public sealed class SourceContainerReference : IAsyncDisposable
{
	public static readonly SourceContainerReference Instance = new();

	private const ushort HostPort = 32774;
	private const string DatabaseName = "testpoc_source";
	private const string Username = "sourceuser";
	private const string Password = "sourcepass";
	private const int SeedRowCount = 30;

	private readonly LazyAsyncStartup<SourceContainerReference> _initializer;
	private PostgreSqlContainer? _container;
	private string? _connectionString;

	public SourceContainerReference()
	{
		_initializer = new LazyAsyncStartup<SourceContainerReference>(
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
		return ValueTask.CompletedTask;
	}

	private async Task StartContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithDatabase(DatabaseName)
			.WithUsername(Username)
			.WithPassword(Password)
			.WithPortBinding(HostPort, 5432)
			.WithLabel("com.docker.compose.project", "testpoc")
			.WithLabel("com.docker.compose.service", "postgres-source")
			.WithReuse(true)
			.Build();

		await _container.StartAsync(cancellationToken);
		_connectionString = _container.GetConnectionString();

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(_connectionString)
			.Options;

		await using var db = new AppDbContext(options);
		await db.Database.MigrateAsync(cancellationToken);

		if (!await db.Items.AnyAsync(cancellationToken))
		{
			db.Items.AddRange(GenerateDummyItems());
			await db.SaveChangesAsync(cancellationToken);
		}
	}

	private static IEnumerable<Item> GenerateDummyItems()
	{
		var faker = new Faker
		{
			Random = new Randomizer(12345)
		};

		var seedInstant = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		return Enumerable.Range(1, SeedRowCount)
			.Select(i => new Item
			{
				Id = faker.Random.Guid(),
				Name = faker.Commerce.ProductName(),
				Price = faker.Random.Decimal(10m, 500m),
				CreatedAt = seedInstant.AddMinutes(i),
			});
	}
}
