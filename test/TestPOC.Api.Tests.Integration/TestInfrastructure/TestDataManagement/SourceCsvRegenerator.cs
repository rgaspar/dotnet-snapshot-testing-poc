using Npgsql;
using TestPOC.TestInfrastructure.Containers;
using TestPOC.TestInfrastructure.Data;
using TestPOC.TestInfrastructure.IO;
using Xunit;

// ReSharper disable UnusedMember.Local
namespace TestPOC.Api.Tests.Integration.TestInfrastructure.TestDataManagement;

/// <summary>
/// Dev tool for regenerating checked-in CSV test datasets from a real source
/// database. Runs manually (facts are Skip-marked by default). Injecting
/// <see cref="SourceContainerReference"/> ensures the "prod-like source"
/// Postgres container is up even when this fact runs in isolation
/// (e.g. <c>dotnet test --filter "FullyQualifiedName~RegenerateItemsCsv"</c>) —
/// otherwise the ApiTestFactory-driven boot path never runs and the source
/// port (32774) is not listening.
///
/// Workflow:
/// 1. Add a fresh entry to <see cref="TablesToExport"/> or <see cref="FilterQueries"/>.
/// 2. Remove the <c>Skip</c> argument from the matching fact and run it once.
/// 3. Restore the <c>Skip</c>, commit the regenerated CSV in
///    <c>TestInfrastructure/TestDatasets/</c>.
/// </summary>
public sealed class SourceCsvRegenerator(SourceContainerReference sourceContainer) : IAsyncLifetime
{
	private static readonly List<string> TablesToExport =
	[
		// Add table names to export as-is. Remove entries after use to avoid
		// accidentally overwriting curated CSVs.
		// "items",
	];

	public async Task InitializeAsync() => await sourceContainer.EnsureInitializedAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact(Skip = "Enable only when regenerating test datasets from a source DB.")]
	public async Task RegenerateFromTables()
	{
		await using var connection = new NpgsqlConnection(sourceContainer.GetConnectionString());
		await connection.OpenAsync();
		await SeedDatasetLoader.ExportTablesAsync(TablesToExport, DatasetsDirectory(), connection);
	}

	//[Fact(Skip = "Enable only when regenerating test datasets from a source DB.")]
	[Fact]
	public async Task RegenerateItemsCsv()
	{
		var sqlToExport = new List<CsvExportSpec>
		{
			new("items", "SELECT \"Id\", \"Name\", \"Price\", \"CreatedAt\" FROM items ORDER BY \"CreatedAt\" DESC LIMIT 5"),
		};

		await using var connection = new NpgsqlConnection(sourceContainer.GetConnectionString());
		await connection.OpenAsync();
		await SeedDatasetLoader.ExportSqlAsync(sqlToExport, DatasetsDirectory(), connection);
	}

	private static string DatasetsDirectory()
		=> ProjectFileLookup.GetProjectPath("/TestInfrastructure/TestDatasets");

	private static class FilterQueries
	{
		public const string RecentItems = """
			SELECT "Id", "Name", "Price", "CreatedAt"
			FROM items
			ORDER BY "CreatedAt" DESC
			LIMIT 200
			""";
	}
}
