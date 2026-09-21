namespace TestPOC.Api.Infrastructure.Persistence.Entities;

public sealed class Item
{
	public Guid Id { get; set; }
	public string Name { get; set; } = null!;
	public decimal Price { get; set; }
	public DateTime CreatedAt { get; set; }
}
