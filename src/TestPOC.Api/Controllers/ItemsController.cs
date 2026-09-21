using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestPOC.Api.Dtos;
using TestPOC.Api.Extensions;
using TestPOC.Api.Services;

namespace TestPOC.Api.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController(IItemsService itemsService) : ControllerBase
{
	private readonly IItemsService _itemsService = itemsService;

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ItemDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<ActionResult<IReadOnlyList<ItemDto>?>> GetItems(CancellationToken cancellationToken)
	{
		var result = await _itemsService.GetAllAsync(cancellationToken);

		return this.ToActionResult(result.Map(items => items is null ? null :
			(IReadOnlyList<ItemDto>)items.Select(item => new ItemDto(item.Id, item.Name, item.Price, item.CreatedAt)).ToList()));
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ItemDto>> GetItemById(Guid id, CancellationToken cancellationToken)
	{
		var result = await _itemsService.GetByIdAsync(id, cancellationToken);

		return this.ToActionResult(result.Map(item => new ItemDto(item.Id, item.Name, item.Price, item.CreatedAt)));
	}

	[HttpPost]
	[Authorize]
	[ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<ItemDto>> CreateItem([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
	{
		var result = (await _itemsService.CreateAsync(request.Name, request.Price, cancellationToken))
			.Map(item => new ItemDto(item.Id, item.Name, item.Price, item.CreatedAt));

		if (result.IsSuccess && result.Value is not null)
		{
			return CreatedAtAction(nameof(GetItemById), new { id = result.Value.Id }, result.Value);
		}

		return this.ToActionResult(result);
	}
}
