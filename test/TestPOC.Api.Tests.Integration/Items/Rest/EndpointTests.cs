using TestPOC.Api.Tests.Integration.TestInfrastructure;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.Rest;

/// <summary>
/// REST endpoint tests for /api/items — GET (list, by id) and POST (create).
/// The parent partial class holds the shared HttpClients; per-endpoint tests
/// live in nested classes in sibling files (EndpointTests.Create.cs,
/// EndpointTests.List.cs, EndpointTests.Get.cs).
///
/// The type is deliberately named "EndpointTests" rather than "MutationTests" /
/// "QueryTests" so it does not collide with the GraphQL side's Mutation/Query
/// naming — those are GraphQL language concepts; on the REST side we only have
/// HTTP endpoints.
/// </summary>
public partial class EndpointTests : IClassFixture<ApiTestFactory>, IAsyncLifetime
{
	protected readonly ApiTestFactory Factory;
	protected readonly HttpClient Client;
	protected readonly HttpClient AuthenticatedClient;

	public EndpointTests(ApiTestFactory factory)
	{
		Factory = factory;
		Client = factory.CreateClient();
		AuthenticatedClient = factory.CreateAuthenticatedClient();
	}

	public Task InitializeAsync() => Factory.ResetDataToSeedAsync();

	public Task DisposeAsync() => Task.CompletedTask;
}
