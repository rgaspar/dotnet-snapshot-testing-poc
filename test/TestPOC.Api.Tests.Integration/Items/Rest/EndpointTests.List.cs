using System.Net;
using FluentAssertions;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.Rest;

public partial class EndpointTests
{
	public class List : EndpointTests
	{
		public List(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_ReturnAllItems_When_NoFilterApplied()
		{
			var response = await Client.GetAsync("/api/items");

			response.StatusCode.Should().Be(HttpStatusCode.OK);
			var body = await response.Content.ReadAsStringAsync();

			SnapshotHelper.ValidateJson(body);
		}
	}
}
