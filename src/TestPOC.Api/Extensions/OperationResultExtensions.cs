using Microsoft.AspNetCore.Mvc;
using TestPOC.Api.Results;

namespace TestPOC.Api.Extensions;

public static class OperationResultExtensions
{
	public static ActionResult<T> ToActionResult<T>(
		this ControllerBase controller,
		OperationResult<T> result)
	{
		if (result.IsSuccess)
		{
			return result.Value is null ? controller.NoContent() : controller.Ok(result.Value);
		}

		return result.Error.ErrorType switch
		{
			ErrorType.NotFound =>
				controller.NotFound(new ProblemDetails
				{
					Title = "Not found",
					Type = result.Error.Type,
					Detail = result.Error.Description,
					Status = StatusCodes.Status404NotFound,
				}),

			ErrorType.Validation =>
				controller.BadRequest(new ProblemDetails
				{
					Title = "Validation error",
					Type = result.Error.Type,
					Detail = result.Error.Description,
					Status = StatusCodes.Status400BadRequest,
				}),

			ErrorType.Conflict =>
				controller.Conflict(new ProblemDetails
				{
					Title = "Conflict",
					Type = result.Error.Type,
					Detail = result.Error.Description,
					Status = StatusCodes.Status409Conflict,
				}),

			ErrorType.Forbidden =>
				controller.StatusCode(StatusCodes.Status403Forbidden),

			_ =>
				controller.Problem(
					detail: result.Error.Description,
					statusCode: StatusCodes.Status500InternalServerError),
		};
	}
}
