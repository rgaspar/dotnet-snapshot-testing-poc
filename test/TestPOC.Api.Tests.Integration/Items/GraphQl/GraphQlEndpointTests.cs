using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.GraphQl;

/// <summary>
/// GraphQL endpoint tests for /graphql — both query and mutation operations.
/// The parent partial class holds the shared HttpClients and ExecuteAsync
/// helpers; per-operation tests live in nested classes in sibling files
/// (GraphQlEndpointTests.Create.cs / .List.cs / .Get.cs).
///
/// The type is deliberately named "GraphQlEndpointTests" (not MutationTests /
/// QueryTests) so it does not collide with the Diligent Entities API test
/// suite that ships those exact type names across many domains.
/// </summary>
public partial class GraphQlEndpointTests : IClassFixture<ApiTestFactory>, IAsyncLifetime
{
	protected readonly ApiTestFactory Factory;
	protected readonly HttpClient Client;
	protected readonly HttpClient AuthenticatedClient;

	public GraphQlEndpointTests(ApiTestFactory factory)
	{
		Factory = factory;
		Client = factory.CreateClient();
		AuthenticatedClient = factory.CreateAuthenticatedClient();
	}

	public Task InitializeAsync() => Factory.ResetDataToSeedAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	protected Task<string> ExecuteAsync(string query, IDictionary<string, object>? variables = null)
		=> ExecuteAsync(AuthenticatedClient, query, variables);

	protected static async Task<string> ExecuteAsync(HttpClient client, string query, IDictionary<string, object>? variables = null)
	{
		var payload = new Dictionary<string, object> { ["query"] = query };
		if (variables is not null)
		{
			payload["variables"] = variables;
		}

		var response = await client.PostAsJsonAsync("/graphql", payload);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		return await response.Content.ReadAsStringAsync();
	}
}
