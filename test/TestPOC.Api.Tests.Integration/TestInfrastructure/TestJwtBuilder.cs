using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TestPOC.Api.Tests.Integration.TestInfrastructure;

/// <summary>
/// Builds a signed JWT using the same symmetric key + issuer + audience the API
/// expects. Tests get real JWT validation end-to-end (no fake handlers), just a
/// pre-signed token they attach as a Bearer header.
/// </summary>
public static class TestJwtBuilder
{
	public const string Issuer = "testpoc";
	public const string Audience = "testpoc-api";
	public const string SigningKey = "dev-signing-key-do-not-use-in-production-32bytes";

	public static string CreateToken(
		string subject = "test-user",
		IEnumerable<Claim>? claims = null,
		TimeSpan? lifetime = null)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var tokenClaims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, subject),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
		};
		if (claims is not null)
		{
			tokenClaims.AddRange(claims);
		}

		var token = new JwtSecurityToken(
			issuer: Issuer,
			audience: Audience,
			claims: tokenClaims,
			notBefore: DateTime.UtcNow,
			expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)),
			signingCredentials: credentials);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
