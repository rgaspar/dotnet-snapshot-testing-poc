using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.TestInfrastructure.IO;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.GraphQl;

public partial class GraphQlEndpointTests
{
	public class List : GraphQlEndpointTests
	{
		private static string Query { get; } = ProjectFileLookup.ReadAllText("Queries/ListItems.graphql");

		public List(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_ReturnAllItems_When_NoFilterApplied()
		{
			var body = await ExecuteAsync(Client, Query);

			SnapshotHelper.ValidateJson(body);
		}
	}
}
