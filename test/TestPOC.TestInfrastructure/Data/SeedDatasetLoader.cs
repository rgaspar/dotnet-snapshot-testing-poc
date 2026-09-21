using System.Data.Common;
using System.Globalization;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.Api.Infrastructure.Persistence.Entities;

namespace TestPOC.TestInfrastructure.Data;

/// <summary>
/// Central entry-point for CSV-backed test datasets:
/// <list type="bullet">
///   <item><see cref="LoadItemsAsync"/> — reads a CSV file and inserts the rows via EF Core (used by ApiTestFactory.ResetDataToSeedAsync).</item>
///   <item><see cref="ExportTablesAsync"/> / <see cref="ExportSqlAsync"/> — dev-only, used from SourceCsvRegenerator to snapshot a source DB into CSV.</item>
/// </list>
/// The dataset directory is passed in by the caller (test project), so this
/// helper stays free of caller-file assumptions and works across projects.
/// </summary>
public static class SeedDatasetLoader
{
	public static CsvConfiguration CsvConfiguration { get; } = new(CultureInfo.InvariantCulture)
	{
		HasHeaderRecord = true,
		ShouldQuote = _ => true,
	};

	/// <summary>
	/// Reads the given <c>items.csv</c> path and inserts the rows into the given DbContext.
	/// Caller is responsible for truncating the target table first if it wants a clean slate.
	/// </summary>
	public static async Task LoadItemsAsync(AppDbContext db, string csvPath, CancellationToken cancellationToken = default)
	{
		await using var reader = new CsvDatasetReader(csvPath);

		var headers = await reader.ReadHeaderAsync(cancellationToken);
		var idIndex = IndexOfHeader(headers, "Id");
		var nameIndex = IndexOfHeader(headers, "Name");
		var priceIndex = IndexOfHeader(headers, "Price");
		var createdAtIndex = IndexOfHeader(headers, "CreatedAt");

		await foreach (var record in reader.ReadRecordsAsync(cancellationToken))
		{
			var createdAt = DateTime.Parse(record[createdAtIndex], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
			db.Items.Add(new Item
			{
				Id = Guid.Parse(record[idIndex]),
				Name = record[nameIndex],
				Price = decimal.Parse(record[priceIndex], CultureInfo.InvariantCulture),
				CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
			});
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Convenience wrapper: dumps each table with <c>SELECT * FROM "table"</c> into
	/// <paramref name="exportDirectory"/>.
	/// </summary>
	public static Task ExportTablesAsync(List<string> tables, string exportDirectory, DbConnection connection, CancellationToken cancellationToken = default)
	{
		var items = tables.Select(t => new CsvExportSpec(t, $"SELECT * FROM \"{t}\"")).ToList();
		return ExportSqlAsync(items, exportDirectory, connection, cancellationToken);
	}

	/// <summary>
	/// Runs each SQL script against <paramref name="connection"/> and writes the
	/// resulting rows to <c>{exportDirectory}/{item.Name}.csv</c>.
	/// </summary>
	public static async Task ExportSqlAsync(List<CsvExportSpec> exports, string exportDirectory, DbConnection connection, CancellationToken cancellationToken = default)
	{
		foreach (var item in exports)
		{
			await using var command = connection.CreateCommand();
			command.CommandText = item.Sql;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			await using var writer = new CsvDatasetWriter(exportDirectory);
			await writer.WriteDataAsync(item, reader, cancellationToken);
		}
	}

	private static int IndexOfHeader(string[] headers, string name)
	{
		for (var i = 0; i < headers.Length; i++)
		{
			if (string.Equals(headers[i], name, StringComparison.Ordinal))
			{
				return i;
			}
		}
		throw new InvalidOperationException($"CSV is missing required column '{name}'.");
	}
}
