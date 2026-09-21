using System.Net;
using FluentAssertions;
using HotChocolate.Language;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Schema;

/// <summary>
/// Schema-level snapshot tests. These are cross-cutting: they fail whenever
/// any controller or GraphQL type changes shape, adds/removes an endpoint,
/// or breaks a contract. Domain-specific response-body snapshots live in the
/// per-domain folders (e.g. Items/).
/// </summary>
public sealed class SchemaTests : IClassFixture<ApiTestFactory>
{
	private readonly HttpClient _client;

	public SchemaTests(ApiTestFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task OpenApiJson_ExposesStableRestSchema()
	{
		var response = await _client.GetAsync("/swagger/v1/swagger.json");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync();

		SnapshotHelper.ValidateJsonMatches(body, "__snapshots__/openapi.json");
	}

	[Fact]
	public async Task GraphQlSdl_ExposesStableSchema()
	{
		var response = await _client.GetAsync("/graphql?sdl");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var sdl = await response.Content.ReadAsStringAsync();
		var actualDocument = Utf8GraphQLParser.Parse(sdl, new ParserOptions(noLocations: true));

		SnapshotHelper.ValidateGraphQlSchemaMatches(actualDocument, "__snapshots__/schema.graphql");
	}
}
