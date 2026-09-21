namespace TestPOC.TestInfrastructure.Data;

/// <summary>
/// A named SQL script that can be exported from a source database into a CSV
/// </summary>
public sealed record CsvExportSpec(string Name, string Sql);
