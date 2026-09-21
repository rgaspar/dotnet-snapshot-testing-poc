using HotChocolate;
using HotChocolate.Authorization;
using TestPOC.Api.Infrastructure.Persistence.Entities;
using TestPOC.Api.Services;

namespace TestPOC.Api.GraphQl;

public sealed class Mutation
{
	[Authorize]
	public async Task<Item> CreateItem(CreateItemInput input, [Service] IItemsService itemsService, CancellationToken cancellationToken)
	{
		var result = await itemsService.CreateAsync(input.Name, input.Price, cancellationToken);

		if (result.IsSuccess)
		{
			return result.Value!;
		}

		throw new GraphQLException(ErrorBuilder.New()
			.SetMessage(result.Error.Description)
			.SetCode(result.Error.Type)
			.Build());
	}
}

public sealed record CreateItemInput(string Name, decimal Price);
