using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TestPOC.Api.Dtos;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;
using TestPOC.TestInfrastructure.Snapshots;
using Xunit;

namespace TestPOC.Api.Tests.Integration.Items.Rest;

public partial class EndpointTests
{
	public class Create : EndpointTests
	{
		public Create(ApiTestFactory factory) : base(factory) { }

		[Fact]
		public async Task Should_CreateItem_When_InputIsValid()
		{
			var input = GenerateInput(ValidItem.NewItemName);

			var response = await AuthenticatedClient.PostAsJsonAsync("/api/items", input);

			response.StatusCode.Should().Be(HttpStatusCode.Created);
			var body = await response.Content.ReadAsStringAsync();
			var stripped = JsonTestHelpers.StripFields(body, "id", "createdAt");

			SnapshotHelper.ValidateJson(stripped);
		}

		[Fact]
		public async Task Should_Return400_When_NameIsMissing()
		{
			var input = GenerateInput(name: string.Empty);

			var response = await AuthenticatedClient.PostAsJsonAsync("/api/items", input);

			response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task Should_Return401_When_BearerTokenIsMissing()
		{
			var input = GenerateInput();

			var response = await Client.PostAsJsonAsync("/api/items", input);

			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		private static CreateItemRequest GenerateInput(
			string name = ValidItem.Name,
			decimal price = ValidItem.Price)
		{
			return new CreateItemRequest
			{
				Name = name,
				Price = price,
			};
		}
	}
}
