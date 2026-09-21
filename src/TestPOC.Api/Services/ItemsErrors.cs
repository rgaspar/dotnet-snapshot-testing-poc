using TestPOC.Api.Results;

namespace TestPOC.Api.Services;

public static class ItemsErrors
{
	public static readonly OperationError NotFound = new(
		"Items.NotFound",
		"The requested item does not exist.",
		ErrorType.NotFound);

	public static readonly OperationError NameAlreadyExists = new(
		"Items.NameAlreadyExists",
		"An item with the given name already exists.",
		ErrorType.Conflict);
}
