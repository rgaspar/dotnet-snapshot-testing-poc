using System.ComponentModel.DataAnnotations;

namespace TestPOC.Api.Dtos;

public sealed class CreateItemRequest
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public required string Name { get; init; }

	[Range(0.0, double.MaxValue)]
	public required decimal Price { get; init; }	
}
