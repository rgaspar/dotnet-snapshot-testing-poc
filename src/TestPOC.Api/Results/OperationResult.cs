namespace TestPOC.Api.Results;

public sealed class OperationResult<T>
{
	private OperationResult(T? value, OperationError error)
	{
		Value = value;
		Error = error;
	}

	public T? Value { get; }

	public OperationError Error { get; }

	public bool IsSuccess => Error == OperationError.None;

	public bool IsFailure => !IsSuccess;

	public static OperationResult<T> Success(T? value)
		=> new(value, OperationError.None);

	public static OperationResult<T> Failure(OperationError error)
		=> new(default, error);

	public OperationResult<TOut> Map<TOut>(Func<T, TOut> selector)
	{
		if (IsFailure)
		{
			return OperationResult<TOut>.Failure(Error);
		}

		return Value is null
			? OperationResult<TOut>.Success(default)
			: OperationResult<TOut>.Success(selector(Value!));
	}
}
