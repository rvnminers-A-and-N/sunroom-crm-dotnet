using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Services;

namespace SunroomCrm.Tests.Unit.Services;

public class TokenServiceTests
{
    private const string SigningKey = "TestSecretKeyThatIsAtLeast32Characters!!";
    private const string Issuer = "TestIssuer";
    private const string Audience = "TestAudience";

    private readonly TokenService _service;

    public TokenServiceTests()
    {
        _service = BuildService();
    }

    private static TokenService BuildService(
        string key = SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        string? expirationHours = "1")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = key,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpirationHours"] = expirationHours
            })
            .Build();

        return new TokenService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var user = new User
        {
            Id = 42,
            Name = "Test User",
            Email = "test@example.com",
            Role = UserRole.Admin
        };

        var token = _service.GenerateToken(user);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ContainsCorrectClaims()
    {
        var user = new User
        {
            Id = 42,
            Name = "Test User",
            Email = "test@example.com",
            Role = UserRole.Manager
        };

        var token = _service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("42");
        jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be("test@example.com");
        jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value.Should().Be("Test User");
        jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be("Manager");
    }

    [Fact]
    public void GenerateToken_SetsCorrectIssuerAndAudience()
    {
        var user = new User { Id = 1, Name = "Test", Email = "t@t.com", Role = UserRole.User };

        var token = _service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Theory]
    [InlineData(UserRole.User, "User")]
    [InlineData(UserRole.Manager, "Manager")]
    [InlineData(UserRole.Admin, "Admin")]
    public void GenerateToken_EncodesEachRoleAsString(UserRole role, string expected)
    {
        var user = new User { Id = 1, Name = "T", Email = "t@t.com", Role = role };

        var token = _service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(expected);
    }

    [Fact]
    public void GenerateToken_UsesConfiguredExpirationHours()
    {
        var service = BuildService(expirationHours: "5");
        var user = new User { Id = 1, Name = "T", Email = "t@t.com", Role = UserRole.User };
        var before = DateTime.UtcNow;

        var token = service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expectedExpiry = before.AddHours(5);
        jwt.ValidTo.Should().BeOnOrAfter(expectedExpiry.AddSeconds(-5));
        jwt.ValidTo.Should().BeOnOrBefore(expectedExpiry.AddSeconds(5));
    }

    [Fact]
    public void GenerateToken_DefaultsExpirationToTwentyFourHours_WhenConfigMissing()
    {
        var service = BuildService(expirationHours: null);
        var user = new User { Id = 1, Name = "T", Email = "t@t.com", Role = UserRole.User };
        var before = DateTime.UtcNow;

        var token = service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expectedExpiry = before.AddHours(24);
        jwt.ValidTo.Should().BeOnOrAfter(expectedExpiry.AddSeconds(-5));
        jwt.ValidTo.Should().BeOnOrBefore(expectedExpiry.AddSeconds(5));
    }

    [Fact]
    public void GenerateToken_UsesHmacSha256Algorithm()
    {
        var user = new User { Id = 1, Name = "T", Email = "t@t.com", Role = UserRole.User };

        var token = _service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.SignatureAlgorithm.Should().Be("HS256");
    }

    [Fact]
    public void GenerateToken_ProducesTokenValidatableWithSameKey()
    {
        var user = new User
        {
            Id = 99,
            Name = "Validatable",
            Email = "v@example.com",
            Role = UserRole.Admin
        };

        var token = _service.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var act = () => handler.ValidateToken(token, parameters, out _);

        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateToken_FailsValidation_WithDifferentKey()
    {
        var user = new User { Id = 1, Name = "T", Email = "t@t.com", Role = UserRole.User };
        var token = _service.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("WrongKeyWrongKeyWrongKeyWrongKey!!")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var act = () => handler.ValidateToken(token, parameters, out _);

        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void GenerateToken_DoesNotIncludePassword()
    {
        var user = new User
        {
            Id = 1,
            Name = "Test",
            Email = "t@t.com",
            Password = "should-never-leak",
            Role = UserRole.User
        };

        var token = _service.GenerateToken(user);

        token.Should().NotContain("should-never-leak");
    }

    [Fact]
    public void GenerateToken_GeneratesUniqueTokensAcrossDifferentUsers()
    {
        var userA = new User { Id = 1, Name = "A", Email = "a@a.com", Role = UserRole.User };
        var userB = new User { Id = 2, Name = "B", Email = "b@b.com", Role = UserRole.User };

        var tokenA = _service.GenerateToken(userA);
        var tokenB = _service.GenerateToken(userB);

        tokenA.Should().NotBe(tokenB);
    }
}
