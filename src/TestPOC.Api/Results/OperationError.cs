namespace TestPOC.Api.Results;

public sealed record OperationError(
	string Type,
	string Description,
	ErrorType ErrorType)
{
	public static readonly OperationError None = new(string.Empty, string.Empty, ErrorType.None);
}
