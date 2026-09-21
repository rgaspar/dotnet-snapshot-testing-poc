using TestPOC.Api.Infrastructure.Persistence.Entities;
using TestPOC.Api.Results;

namespace TestPOC.Api.Services;

public interface IItemsService
{
	Task<OperationResult<IReadOnlyList<Item>?>> GetAllAsync(CancellationToken cancellationToken);

	Task<OperationResult<Item>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

	Task<OperationResult<Item>> CreateAsync(string name, decimal price, CancellationToken cancellationToken);
}
