using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/users endpoints. The whole controller is
/// [Authorize(Roles = "Admin")], so these tests cover both the admin happy
/// paths and the role-based authorization rejection paths for non-admins.
/// </summary>
public class UsersControllerIntegrationTests : IntegrationTestBase
{
    public UsersControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_AsRegularUser_Returns403()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(role: UserRole.User);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_AsManager_Returns403()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(role: UserRole.Manager);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- GET /api/users ----

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsAllUsers()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(name: "Admin", email: "admin@x.com", role: UserRole.Admin);
        await SeedUserAsync(name: "Other1", email: "u1@x.com");
        await SeedUserAsync(name: "Other2", email: "u2@x.com");
        var client = CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await ReadJsonAsync<List<UserDto>>(response);
        users.Should().NotBeNull();
        users!.Should().HaveCount(3);
    }

    // ---- GET /api/users/{id} ----

    [Fact]
    public async Task GetById_AsAdmin_ReturnsUser()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var target = await SeedUserAsync(name: "Target", email: "target@x.com");
        var client = CreateAuthenticatedClient(admin);

        var response = await client.GetAsync($"/api/users/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<UserDto>(response);
        dto!.Email.Should().Be("target@x.com");
    }

    [Fact]
    public async Task GetById_NonexistentUser_Returns404()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var client = CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/users/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- PUT /api/users/{id} ----

    [Fact]
    public async Task Update_AsAdmin_ChangesNameAndRole()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var target = await SeedUserAsync(name: "Old", email: "t@x.com", role: UserRole.User);
        var client = CreateAuthenticatedClient(admin);

        var response = await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            name = "New",
            role = "Manager"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<UserDto>(response);
        dto!.Name.Should().Be("New");
        dto.Role.Should().Be("Manager");

        await using var db = Factory.CreateDbContext();
        var updated = db.Users.Single(u => u.Id == target.Id);
        updated.Name.Should().Be("New");
        updated.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public async Task Update_AsAdmin_NonexistentUser_Returns404()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var client = CreateAuthenticatedClient(admin);

        var response = await client.PutAsJsonAsync("/api/users/99999", new { name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsRegularUser_Returns403()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(role: UserRole.User);
        var target = await SeedUserAsync(email: "victim@x.com");
        var client = CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/users/{target.Id}", new { name = "Hacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- DELETE /api/users/{id} ----

    [Fact]
    public async Task Delete_AsAdmin_RemovesUser_AndReturns204()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var target = await SeedUserAsync(email: "del@x.com");
        var client = CreateAuthenticatedClient(admin);

        var response = await client.DeleteAsync($"/api/users/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = Factory.CreateDbContext();
        db.Users.Any(u => u.Id == target.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonexistentUser_Returns404()
    {
        await ResetDatabaseAsync();
        var admin = await SeedUserAsync(role: UserRole.Admin);
        var client = CreateAuthenticatedClient(admin);

        var response = await client.DeleteAsync("/api/users/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AsRegularUser_Returns403()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(role: UserRole.User);
        var target = await SeedUserAsync(email: "victim@x.com");
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/users/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
