using Microsoft.EntityFrameworkCore;
using TestPOC.Api.Infrastructure.Messaging;
using TestPOC.Api.Infrastructure.Messaging.Events;
using TestPOC.Api.Infrastructure.Persistence;
using TestPOC.Api.Infrastructure.Persistence.Entities;
using TestPOC.Api.Results;

namespace TestPOC.Api.Services;

public sealed class ItemsService(
	AppDbContext dbContext,
	TimeProvider timeProvider,
	IEventPublisher eventPublisher) : IItemsService
{
	private readonly AppDbContext _dbContext = dbContext;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly IEventPublisher _eventPublisher = eventPublisher;

	public async Task<OperationResult<IReadOnlyList<Item>?>> GetAllAsync(CancellationToken cancellationToken)
	{
		var items = await _dbContext.Items
			.AsNoTracking()
			.OrderBy(item => item.Name)
			.ToListAsync(cancellationToken);

		return items.Count == 0
			? OperationResult<IReadOnlyList<Item>?>.Success(null)
			: OperationResult<IReadOnlyList<Item>?>.Success(items);
	}

	public async Task<OperationResult<Item>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
	{
		var item = await _dbContext.Items
			.AsNoTracking()
			.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

		return item is null
			? OperationResult<Item>.Failure(ItemsErrors.NotFound)
			: OperationResult<Item>.Success(item);
	}

	public async Task<OperationResult<Item>> CreateAsync(string name, decimal price, CancellationToken cancellationToken)
	{
		var nameTaken = await _dbContext.Items
			.AsNoTracking()
			.AnyAsync(item => item.Name == name, cancellationToken);

		if (nameTaken)
		{
			return OperationResult<Item>.Failure(ItemsErrors.NameAlreadyExists);
		}

		var item = new Item
		{
			Id = Guid.NewGuid(),
			Name = name,
			Price = price,
			CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
		};

		_dbContext.Items.Add(item);
		await _eventPublisher.PublishAsync(
			new ItemCreatedEvent(item.Id, item.Name, item.Price, item.CreatedAt),
			cancellationToken);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return OperationResult<Item>.Success(item);
	}
}
