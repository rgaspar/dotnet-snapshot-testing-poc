using System.Data.Common;
using System.Text;
using CsvHelper;

namespace TestPOC.TestInfrastructure.Data;

/// <summary>
/// Writes rows from a <see cref="DbDataReader"/> to a CSV dataset file.
/// Used only by SourceCsvRegenerator to snapshot a source database into the
/// checked-in TestDatasets folder.
/// </summary>
public sealed class CsvDatasetWriter(string exportDirectory) : IAsyncDisposable
{
	private readonly string _exportDirectory = exportDirectory;

	public async Task WriteDataAsync(CsvExportSpec item, DbDataReader reader, CancellationToken cancellationToken = default)
	{
		var filePath = Path.Combine(_exportDirectory, $"{item.Name}.csv");
		Directory.CreateDirectory(_exportDirectory);

		await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
		using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
		using var csv = new CsvWriter(streamWriter, SeedDatasetLoader.CsvConfiguration);

		for (var i = 0; i < reader.FieldCount; i++)
		{
			csv.WriteField(reader.GetName(i));
		}
		await csv.NextRecordAsync();

		while (await reader.ReadAsync(cancellationToken))
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
				csv.WriteField(value);
			}
			await csv.NextRecordAsync();
		}
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
