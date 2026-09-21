using System.Text;
using System.Text.Json;

namespace TestPOC.TestInfrastructure.Snapshots;

/// <summary>
/// Small JSON utilities used by snapshot tests. Volatile fields (Guid ids,
/// server-generated timestamps) are stripped before the snapshot compare
/// so re-runs are stable without touching production code.
/// </summary>
public static class JsonTestHelpers
{
	public static string StripFields(string json, params string[] fieldNames)
	{
		using var document = JsonDocument.Parse(json);
		var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
		{
			WriteWithout(document.RootElement, writer, fieldNames);
		}

		return Encoding.UTF8.GetString(buffer.ToArray());
	}

	private static void WriteWithout(JsonElement element, Utf8JsonWriter writer, string[] fieldNames)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in element.EnumerateObject())
				{
					if (fieldNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
					{
						continue;
					}

					writer.WritePropertyName(property.Name);
					WriteWithout(property.Value, writer, fieldNames);
				}
				writer.WriteEndObject();
				break;

			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in element.EnumerateArray())
				{
					WriteWithout(item, writer, fieldNames);
				}
				writer.WriteEndArray();
				break;

			default:
				element.WriteTo(writer);
				break;
		}
	}
}
