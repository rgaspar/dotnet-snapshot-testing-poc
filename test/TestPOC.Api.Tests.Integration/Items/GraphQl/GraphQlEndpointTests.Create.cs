using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;
using TestPOC.TestInfrastructure.IO;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.GraphQl;

public partial class GraphQlEndpointTests
{
	public class Create : GraphQlEndpointTests
	{
		private static string Query { get; } = ProjectFileLookup.ReadAllText("Queries/CreateItem.graphql");

		public Create(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_CreateItem_When_InputIsValid()
		{
			var variables = GenerateVariables(ValidItem.NewItemName);

			var body = await ExecuteAsync(Query, variables);
			var stripped = JsonTestHelpers.StripFields(body, "id", "createdAt");

			SnapshotHelper.ValidateJson(stripped);
		}

		[Fact]
		public async Task Should_ReturnAuthError_When_BearerTokenIsMissing()
		{
			var variables = GenerateVariables();

			var body = await ExecuteAsync(Client, Query, variables);

			SnapshotHelper.ValidateJson(body);
		}

		private static Dictionary<string, object> GenerateVariables(
			string name = ValidItem.Name,
			decimal price = ValidItem.Price)
		{
			return new Dictionary<string, object>
			{
				["input"] = new { name, price },
			};
		}
	}
}
