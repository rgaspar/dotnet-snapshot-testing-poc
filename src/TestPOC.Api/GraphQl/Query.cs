using HotChocolate;
using TestPOC.Api.Infrastructure.Persistence.Entities;
using TestPOC.Api.Services;

namespace TestPOC.Api.GraphQl;

public sealed class Query
{
	public async Task<IReadOnlyList<Item>> GetItems([Service] IItemsService itemsService, CancellationToken cancellationToken)
	{
		var result = await itemsService.GetAllAsync(cancellationToken);

		if (result.IsSuccess)
		{
			return result.Value ?? Array.Empty<Item>();
		}

		throw new GraphQLException(ErrorBuilder.New()
			.SetMessage(result.Error.Description)
			.SetCode(result.Error.Type)
			.Build());
	}

	public async Task<Item?> GetItem(Guid id, [Service] IItemsService itemsService, CancellationToken cancellationToken)
	{
		var result = await itemsService.GetByIdAsync(id, cancellationToken);

		if (result.IsSuccess)
		{
			return result.Value;
		}

		throw new GraphQLException(ErrorBuilder.New()
			.SetMessage(result.Error.Description)
			.SetCode(result.Error.Type)
			.Build());
	}
}
