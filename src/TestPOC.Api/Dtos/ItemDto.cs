namespace TestPOC.Api.Dtos;

public sealed record ItemDto(Guid Id, string Name, decimal Price, DateTime CreatedAt);
