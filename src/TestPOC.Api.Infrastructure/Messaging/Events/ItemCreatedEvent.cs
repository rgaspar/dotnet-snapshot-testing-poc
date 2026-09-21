namespace TestPOC.Api.Infrastructure.Messaging.Events;

public sealed record ItemCreatedEvent(Guid Id, string Name, decimal Price, DateTime CreatedAt);
