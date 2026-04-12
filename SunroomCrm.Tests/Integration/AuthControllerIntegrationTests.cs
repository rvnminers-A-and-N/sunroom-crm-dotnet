using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// End-to-end integration tests for /api/auth endpoints. Exercises the full
/// ASP.NET Core pipeline (model binding, validation, repository, BCrypt
/// hashing, JWT issuance) against a fresh in-memory database.
/// </summary>
public class AuthControllerIntegrationTests : IntegrationTestBase
{
    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- POST /api/auth/register ----

    [Fact]
    public async Task Register_NewUser_Returns201_WithTokenAndUserDto()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "New User",
            email = "newuser@example.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync<AuthResponse>(response);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("newuser@example.com");
        body.User.Name.Should().Be("New User");
        body.User.Role.Should().Be("User");
        body.User.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_HashesPassword_NotStoredAsPlaintext()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Hashed",
            email = "hashed@example.com",
            password = "plaintext-secret"
        });

        await using var db = Factory.CreateDbContext();
        var user = db.Users.Single(u => u.Email == "hashed@example.com");
        user.Password.Should().NotBe("plaintext-secret");
        BCrypt.Net.BCrypt.Verify("plaintext-secret", user.Password).Should().BeTrue();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await ResetDatabaseAsync();
        await SeedUserAsync(email: "dup@example.com");
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Dupe",
            email = "dup@example.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_AssignsUserRole_ByDefault()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Default Role",
            email = "defrole@example.com",
            password = "password123"
        });

        var body = await ReadJsonAsync<AuthResponse>(response);
        body!.User.Role.Should().Be("User");

        await using var db = Factory.CreateDbContext();
        db.Users.Single(u => u.Email == "defrole@example.com").Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task Register_WithMissingFields_Returns400()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "noname@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- POST /api/auth/login ----

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_WithToken()
    {
        await ResetDatabaseAsync();
        await SeedUserAsync(name: "Login User", email: "login@example.com", password: "password123");
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login@example.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<AuthResponse>(response);
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("login@example.com");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await ResetDatabaseAsync();
        await SeedUserAsync(email: "pw@example.com", password: "correctpassword");
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "pw@example.com",
            password = "wrongpassword"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_TokenAllowsAccessToProtectedEndpoint()
    {
        await ResetDatabaseAsync();
        await SeedUserAsync(name: "Flow User", email: "flow@example.com", password: "password123");
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "flow@example.com",
            password = "password123"
        });
        var auth = await ReadJsonAsync<AuthResponse>(loginResponse);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);
        var meResponse = await client.GetAsync("/api/auth/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await ReadJsonAsync<UserDto>(meResponse);
        me!.Email.Should().Be("flow@example.com");
    }

    // ---- POST /api/auth/logout ----

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns200()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(email: "logout@example.com");
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET /api/auth/me ----

    [Fact]
    public async Task Me_WithoutAuthHeader_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(email: "exp@example.com");
        var client = CreateClientWithExpiredToken(user);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithMalformedToken_Returns401()
    {
        var client = CreateClientWithMalformedToken();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(name: "Me User", email: "me@example.com", role: UserRole.Manager);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<UserDto>(response);
        dto!.Id.Should().Be(user.Id);
        dto.Email.Should().Be("me@example.com");
        dto.Role.Should().Be("Manager");
    }

    [Fact]
    public async Task Me_AfterUserDeleted_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(email: "ghost@example.com");
        var client = CreateAuthenticatedClient(user);

        // Delete user from DB but keep using the (still valid) token
        await using (var db = Factory.CreateDbContext())
        {
            db.Users.Remove(db.Users.Single(u => u.Id == user.Id));
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
