using System.Net;
using FluentAssertions;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.Rest;

public partial class EndpointTests
{
	public class Get : EndpointTests
	{
		public Get(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_ReturnItem_When_IdMatches()
		{
			var response = await Client.GetAsync($"/api/items/{ValidItem.Id}");

			response.StatusCode.Should().Be(HttpStatusCode.OK);
			var body = await response.Content.ReadAsStringAsync();

			SnapshotHelper.ValidateJson(body);
		}

		[Fact]
		public async Task Should_Return404_When_IdDoesNotExist()
		{
			var response = await Client.GetAsync($"/api/items/{ValidItem.MissingId}");

			response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		}
	}
}
