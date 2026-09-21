using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace TestPOC.TestInfrastructure.Data;

/// <summary>
/// Streams rows from a CSV dataset. Header row is required; each record is
/// exposed as an <see cref="IReadOnlyList{String}"/> keyed by column ordinal.
/// </summary>
public sealed class CsvDatasetReader : IAsyncDisposable
{
	private readonly StreamReader _reader;
	private readonly CsvReader _csv;

	private string[]? _headers;

	public CsvDatasetReader(string csvPath)
	{
		_reader = new StreamReader(csvPath, Encoding.UTF8);
		_csv = new CsvReader(_reader, SeedDatasetLoader.CsvConfiguration);
	}

	public async Task<string[]> ReadHeaderAsync(CancellationToken cancellationToken)
	{
		if (_headers is not null)
		{
			return _headers;
		}

		cancellationToken.ThrowIfCancellationRequested();
		await _csv.ReadAsync();
		if (!_csv.ReadHeader())
		{
			throw new InvalidOperationException("CSV file has no header row.");
		}

		_headers = _csv.HeaderRecord ?? throw new InvalidOperationException("CSV header could not be parsed.");
		return _headers;
	}

	public async IAsyncEnumerable<IReadOnlyList<string>> ReadRecordsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_headers is null)
		{
			await ReadHeaderAsync(cancellationToken);
		}

		while (await _csv.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			var record = new string[_headers!.Length];
			for (var i = 0; i < _headers.Length; i++)
			{
				record[i] = _csv.GetField(i) ?? string.Empty;
			}
			yield return record;
		}
	}

	public ValueTask DisposeAsync()
	{
		_csv.Dispose();
		_reader.Dispose();
		return ValueTask.CompletedTask;
	}
}
