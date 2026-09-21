namespace TestPOC.Api.Tests.Integration.TestInfrastructure.TestData;

/// <summary>
/// Canonical valid Item test data. All fields are const so they can be used as
/// default parameter values in <c>GenerateInput</c> factories inside per-operation
/// test classes — a test overrides one field at a time to exercise edge cases
/// </summary>
public static class ValidItem
{
	public const string Id = "2679d4d8-2208-872c-4446-6ac95eaecfc3";
	public const string OtherSeededId = "22222222-2222-2222-2222-222222222222";
	public const string MissingId = "99999999-9999-9999-9999-999999999999";

	public const string Name = "Widget";
	public const decimal Price = 19.99m;
	public const string NewItemName = "Sprocket";

	public const string OtherSeededName = "Gadget";
	public const decimal OtherSeededPrice = 49.50m;

	public const string CreatedAtUtc = "2026-01-01T00:00:00Z";
}
