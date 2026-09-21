namespace TestPOC.Api.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
	public Guid Id { get; set; }
	public string Type { get; set; } = null!;
	public string Payload { get; set; } = null!;
	public DateTime CreatedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public int Attempts { get; set; }
}
