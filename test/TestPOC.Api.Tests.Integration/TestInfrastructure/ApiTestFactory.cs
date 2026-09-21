using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.TestInfrastructure.Containers;
using TestPOC.TestInfrastructure.Data;
using TestPOC.TestInfrastructure.IO;
using Xunit;

namespace TestPOC.Api.Tests.Integration.TestInfrastructure;

/// <summary>
/// WebApplicationFactory that points the API's DbContext at the shared
/// PostgreSQL Testcontainer. Registered as a singleton in <see cref="Startup"/>,
/// so every test class shares the same host + container.
///
/// **Debug-run fast path:** when the test host is running under a debugger the
/// container is reused (see <see cref="SharedPostgresContainerReference"/>) and
/// <see cref="ResetDataToSeedAsync"/> is a no-op. The EF migration always runs
/// on host boot (it is idempotent — no-op if the schema already exists), so
/// the first debug run on a fresh container also brings the schema up.
/// Every subsequent debug iteration starts against the DB rows left behind by
/// the previous run, so you can inspect state or attach new tests without
/// re-seeding. In CI / plain <c>dotnet test</c> the container is fresh and the
/// seed is loaded before each test class.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private static readonly bool DebugFastPath = Debugger.IsAttached;

	public async Task InitializeAsync()
	{
		await SharedPostgresContainerReference.Instance.EnsureInitializedAsync();
		await RabbitMqTestContainerReference.Instance.EnsureInitializedAsync();

		// Also start the "prod-like source" container so `dotnet test` runs can
		// demonstrate the SourceCsvRegenerator workflow without needing
		// `dotnet run` first. See SourceContainerReference for the note.
		await SourceContainerReference.Instance.EnsureInitializedAsync();

		// Force host build. Program.cs Database.MigrateAsync() runs against the
		// container — idempotent, brings the schema up on first run and no-ops afterwards.
		using var _ = CreateClient();
	}

	public string RabbitMqConnectionString => RabbitMqTestContainerReference.Instance.GetConnectionString();

	/// <summary>
	/// Creates an HttpClient pre-attached with a Bearer token signed by
	/// <see cref="TestJwtBuilder"/>. The API validates it with the same
	/// symmetric key + issuer + audience (see JWT config override in
	/// <see cref="ConfigureWebHost"/>), so this is a real end-to-end auth
	/// path — no fake auth handler.
	/// </summary>
	public HttpClient CreateAuthenticatedClient(string subject = "test-user")
	{
		var client = CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", TestJwtBuilder.CreateToken(subject: subject));
		return client;
	}

	Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// Anything other than "Development" — keeps dev-only hosted services
		// out of the test host.
		builder.UseEnvironment("Testing");

		builder.ConfigureAppConfiguration(config =>
		{
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Default"] = SharedPostgresContainerReference.Instance.GetConnectionString(),
				["ConnectionStrings:RabbitMq"] = RabbitMqTestContainerReference.Instance.GetConnectionString(),
				["Jwt:Issuer"] = TestJwtBuilder.Issuer,
				["Jwt:Audience"] = TestJwtBuilder.Audience,
				["Jwt:SigningKey"] = TestJwtBuilder.SigningKey,
			});
		});
	}

	/// <summary>
	/// Truncates the Items table and re-loads the canonical seed rows from
	/// <c>TestInfrastructure/TestDatasets/items.csv</c>. Called by each test
	/// class's InitializeAsync so tests always start from a known-good state,
	/// regardless of what previous tests inserted or deleted.
	///
	/// Under a debugger this is a no-op: the rows from the previous debug
	/// iteration are preserved so you can inspect what a test left behind.
	/// </summary>
	public async Task ResetDataToSeedAsync()
	{
		if (DebugFastPath)
		{
			return;
		}

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

		await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE items RESTART IDENTITY CASCADE;");
		var csvPath = ProjectFileLookup.GetProjectPath("/TestInfrastructure/TestDatasets/items.csv");
		await SeedDatasetLoader.LoadItemsAsync(db, csvPath);
	}
}
