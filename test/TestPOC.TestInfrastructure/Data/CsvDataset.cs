namespace TestPOC.TestInfrastructure.Data;

/// <summary>
/// Represents a named CSV file in <c>TestInfrastructure/TestDatasets</c> whose
/// rows are imported into the Testcontainer database before tests run.
/// </summary>
public readonly record struct CsvDataset(string Name)
{
	public override string ToString() => Name;
}
