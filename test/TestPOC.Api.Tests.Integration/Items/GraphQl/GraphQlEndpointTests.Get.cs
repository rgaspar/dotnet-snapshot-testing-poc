using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;
using TestPOC.TestInfrastructure.IO;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.GraphQl;

public partial class GraphQlEndpointTests
{
	public class Get : GraphQlEndpointTests
	{
		private static string Query { get; } = ProjectFileLookup.ReadAllText("Queries/GetItem.graphql");

		public Get(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_ReturnItem_When_IdMatches()
		{
			var variables = GenerateVariables();

			var body = await ExecuteAsync(Client, Query, variables);

			SnapshotHelper.ValidateJson(body);
		}

		[Fact]
		public async Task Should_ReturnNull_When_IdDoesNotExist()
		{
			var variables = GenerateVariables(id: ValidItem.MissingId);

			var body = await ExecuteAsync(Client, Query, variables);

			SnapshotHelper.ValidateJson(body);
		}

		private static Dictionary<string, object> GenerateVariables(string id = ValidItem.Id)
		{
			return new Dictionary<string, object> { ["id"] = id };
		}
	}
}
