using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using HotChocolate.Language;
using HotChocolate.Language.Utilities;
using TestPOC.TestInfrastructure.IO;
using Xunit.Sdk;

namespace TestPOC.TestInfrastructure.Snapshots;

/// <summary>
/// Hand-rolled snapshot testing helper.
///
/// Workflow:
///   1. Test runs and produces an actual value (string / JSON / GraphQL DocumentNode).
///   2. If no snapshot file exists yet, one is created on the fly and the test passes.
///      Commit the generated snapshot alongside the test.
///   3. If a snapshot exists, actual is compared against it.
///      - Match: test passes.
///      - Mismatch: the actual value is written to a sibling '__MISMATCH__' directory,
///        and the test fails. Diff the mismatch file against the snapshot; if the
///        change is intentional, overwrite the snapshot with the mismatch content
///        (and delete the __MISMATCH__ file). Otherwise, fix the code.
/// </summary>
public static class SnapshotHelper
{
	private static readonly JsonSerializerOptions PrettyJsonOptions = new()
	{
		WriteIndented = true,
	};

	/// <summary>
	/// Snapshot-compare a JSON payload. Both actual and stored snapshot are canonicalized
	/// (pretty-printed with sorted keys) before comparison so whitespace/key-order do not
	/// cause false negatives.
	/// </summary>
	public static void ValidateJsonMatches(string actualJson, string snapshotFile, [CallerFilePath] string baseFilePath = null!)
	{
		var normalized = NormalizeJson(actualJson);
		ValidateTextMatches(normalized, snapshotFile, baseFilePath);
	}

	/// <summary>
	/// Snapshot-compare a JSON payload with the snapshot file path derived from the
	/// caller: <c>__snapshots__/{operation}.{methodName}.json</c>, where
	/// <c>operation</c> is the caller file's last dot-segment (e.g.
	/// <c>EndpointTests.Create.cs</c> → <c>Create</c>, <c>MutationTests.cs</c> →
	/// <c>MutationTests</c>). Use this from a test-method body; for snapshots that
	/// don't follow the per-test convention (e.g. openapi.json, schema.graphql),
	/// call <see cref="ValidateJsonMatches(string, string, string)"/> with an
	/// explicit path.
	/// </summary>
	public static void ValidateJson(
		string actualJson,
		[CallerMemberName] string methodName = "",
		[CallerFilePath] string baseFilePath = "")
	{
		var snapshotFile = BuildAutoSnapshotPath(baseFilePath, methodName, ".json");
		ValidateJsonMatches(actualJson, snapshotFile, baseFilePath);
	}

	private static string BuildAutoSnapshotPath(string baseFilePath, string methodName, string extension)
	{
		var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
		var lastDot = fileName.LastIndexOf('.');
		var operation = lastDot >= 0 ? fileName[(lastDot + 1)..] : fileName;
		return $"__snapshots__/{operation}.{methodName}{extension}";
	}

	/// <summary>
	/// Snapshot-compare a GraphQL schema DocumentNode.
	/// </summary>
	public static void ValidateGraphQlSchemaMatches(DocumentNode actualSchema, string snapshotFile, [CallerFilePath] string baseFilePath = null!)
	{
		var actualSdl = actualSchema.Print();

		if (!ProjectFileLookup.Exists(snapshotFile, baseFilePath))
		{
			ProjectFileLookup.WriteAllText(snapshotFile, actualSdl, baseFilePath);
			return;
		}

		var expectedSdl = ProjectFileLookup.ReadAllText(snapshotFile, baseFilePath);

		try
		{
			var expectedSchema = Utf8GraphQLParser.Parse(expectedSdl, new ParserOptions(noLocations: true));

			actualSchema.Should()
				.BeEquivalentTo(
					expectedSchema,
					options => options.Excluding(node => node.Count)
						.RespectingRuntimeTypes()
						.WithoutStrictOrderingFor(node => node.Definitions));
		}
		catch (XunitException ex)
		{
			var mismatchPath = WriteMismatchFile(actualSdl, snapshotFile, baseFilePath);

			throw new XunitException(BuildMismatchMessage(snapshotFile, baseFilePath, mismatchPath, expectedSdl, actualSdl, ex.Message));
		}
	}

	/// <summary>
	/// Snapshot-compare raw text. Prefer <see cref="ValidateJsonMatches"/> for JSON content.
	/// </summary>
	public static void ValidateTextMatches(string actualText, string snapshotFile, [CallerFilePath] string baseFilePath = null!)
	{
		if (!ProjectFileLookup.Exists(snapshotFile, baseFilePath))
		{
			ProjectFileLookup.WriteAllText(snapshotFile, actualText, baseFilePath);
			return;
		}

		var expected = ProjectFileLookup.ReadAllText(snapshotFile, baseFilePath);
		var expectedNormalized = NormalizeLineEndings(expected);
		var actualNormalized = NormalizeLineEndings(actualText);

		if (expectedNormalized == actualNormalized)
		{
			return;
		}

		var mismatchPath = WriteMismatchFile(actualText, snapshotFile, baseFilePath);

		throw new XunitException(BuildMismatchMessage(snapshotFile, baseFilePath, mismatchPath, expectedNormalized, actualNormalized, extraDetails: null));
	}

	private static string NormalizeJson(string json)
	{
		using var document = JsonDocument.Parse(json);
		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
		{
			WriteSorted(document.RootElement, writer);
		}

		return System.Text.Encoding.UTF8.GetString(stream.ToArray());
	}

	private static void WriteSorted(JsonElement element, Utf8JsonWriter writer)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
				{
					writer.WritePropertyName(property.Name);
					WriteSorted(property.Value, writer);
				}
				writer.WriteEndObject();
				break;

			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in element.EnumerateArray())
				{
					WriteSorted(item, writer);
				}
				writer.WriteEndArray();
				break;

			default:
				element.WriteTo(writer);
				break;
		}
	}

	private static string NormalizeLineEndings(string text)
	{
		return text.Replace("\r\n", "\n").TrimEnd();
	}

	private static string WriteMismatchFile(string content, string snapshotFile, string baseFilePath)
	{
		var snapshotPath = ProjectFileLookup.GetProjectPath(snapshotFile, baseFilePath);
		var snapshotInfo = new FileInfo(snapshotPath);
		var mismatchDir = Path.Combine(snapshotInfo.Directory!.FullName, "__MISMATCH__");

		if (!Directory.Exists(mismatchDir))
		{
			Directory.CreateDirectory(mismatchDir);
		}

		var mismatchFile = Path.Combine(mismatchDir, snapshotInfo.Name);
		File.WriteAllText(mismatchFile, content);
		return mismatchFile;
	}

	private static string BuildMismatchMessage(
		string snapshotFile,
		string baseFilePath,
		string mismatchPath,
		string expected,
		string actual,
		string? extraDetails)
	{
		var snapshotPath = ProjectFileLookup.GetProjectPath(snapshotFile, baseFilePath);
		var diff = BuildLineDiff(expected, actual);

		var sb = new System.Text.StringBuilder();
		sb.AppendLine($"Snapshot mismatch: {snapshotFile}");
		sb.AppendLine();
		sb.AppendLine($"  Expected snapshot: {snapshotPath}");
		sb.AppendLine($"  Actual (mismatch): {mismatchPath}");
		sb.AppendLine();
		sb.AppendLine(diff);
		sb.AppendLine();
		sb.AppendLine("If the change is intentional, overwrite the snapshot with the mismatch file, then delete the __MISMATCH__ file.");

		if (!string.IsNullOrWhiteSpace(extraDetails))
		{
			sb.AppendLine();
			sb.AppendLine("Structural diff (FluentAssertions):");
			sb.AppendLine(extraDetails);
		}

		return sb.ToString();
	}

	private static string BuildLineDiff(string expected, string actual)
	{
		const int MaxLinesPerSide = 20;

		var expectedLines = expected.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
		var actualLines = actual.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();

		var expectedSet = new HashSet<string>(expectedLines);
		var actualSet = new HashSet<string>(actualLines);

		var removed = expectedLines.Where(l => !actualSet.Contains(l) && l.Length > 0).ToList();
		var added = actualLines.Where(l => !expectedSet.Contains(l) && l.Length > 0).ToList();

		var sb = new System.Text.StringBuilder();
		sb.AppendLine("--- Snapshot has, response does not (removed):");
		if (removed.Count == 0)
		{
			sb.AppendLine("  (none)");
		}
		else
		{
			foreach (var line in removed.Take(MaxLinesPerSide))
			{
				sb.AppendLine($"  - {line}");
			}
			if (removed.Count > MaxLinesPerSide)
			{
				sb.AppendLine($"  ... and {removed.Count - MaxLinesPerSide} more removed line(s).");
			}
		}

		sb.AppendLine();
		sb.AppendLine("+++ Response has, snapshot does not (added):");
		if (added.Count == 0)
		{
			sb.AppendLine("  (none)");
		}
		else
		{
			foreach (var line in added.Take(MaxLinesPerSide))
			{
				sb.AppendLine($"  + {line}");
			}
			if (added.Count > MaxLinesPerSide)
			{
				sb.AppendLine($"  ... and {added.Count - MaxLinesPerSide} more added line(s).");
			}
		}

		return sb.ToString();
	}
}
