using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Tests.Helpers;

/// <summary>
/// Generates JWT tokens for integration tests using the same signing key
/// configured in <c>appsettings.Test.json</c>. This helper exists so tests
/// can produce valid bearer tokens without going through the real
/// <c>POST /api/auth/login</c> flow when they only need an authenticated client.
/// </summary>
public static class JwtTestHelper
{
    public const string TestSigningKey = "TestSigningKey_DoNotUseInProduction_MustBeAtLeast32Chars!!";
    public const string TestIssuer = "SunroomCrmTest";
    public const string TestAudience = "SunroomCrmTestAudience";

    public static string GenerateToken(User user, DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateExpiredToken(User user)
    {
        return GenerateToken(user, expires: DateTime.UtcNow.AddHours(-1));
    }

    public static string GenerateMalformedToken()
    {
        return "this.is.not.a.valid.jwt";
    }
}
