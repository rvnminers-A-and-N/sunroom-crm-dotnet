using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/tags endpoints. Covers CRUD plus the
/// duplicate-name validation enforced on Create and the
/// "exclude self" duplicate check enforced on Update.
/// </summary>
public class TagsControllerIntegrationTests : IntegrationTestBase
{
    public TagsControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Authenticated_ReturnsAllTags()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedTagAsync("Hot");
        await SeedTagAsync("Cold");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await ReadJsonAsync<List<TagDto>>(response);
        tags!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_PersistsTagAndReturns201()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateTagRequest { Name = "Important", Color = "#FF0000" };

        var response = await client.PostAsJsonAsync("/api/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<TagDto>(response);
        created!.Name.Should().Be("Important");
        created.Color.Should().Be("#FF0000");

        await using var db = Factory.CreateDbContext();
        (await db.Tags.FindAsync(created.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_DuplicateName_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedTagAsync("Existing");
        var client = CreateAuthenticatedClient(user);
        var request = new CreateTagRequest { Name = "Existing", Color = "#123456" };

        var response = await client.PostAsJsonAsync("/api/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidColorFormat_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateTagRequest { Name = "Bad", Color = "not-a-hex" };

        var response = await client.PostAsJsonAsync("/api/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_NonexistentTag_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateTagRequest { Name = "X", Color = "#000000" };

        var response = await client.PutAsJsonAsync("/api/tags/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ChangesNameAndColor()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag = await SeedTagAsync("Old");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateTagRequest { Name = "New", Color = "#ABCDEF" };

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Tags.FindAsync(tag.Id);
        persisted!.Name.Should().Be("New");
        persisted.Color.Should().Be("#ABCDEF");
    }

    [Fact]
    public async Task Update_ToSameName_IsAllowed()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag = await SeedTagAsync("SameName");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateTagRequest { Name = "SameName", Color = "#111111" };

        var response = await client.PutAsJsonAsync($"/api/tags/{tag.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ToOtherTagsName_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag1 = await SeedTagAsync("First");
        await SeedTagAsync("Second");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateTagRequest { Name = "Second", Color = "#222222" };

        var response = await client.PutAsJsonAsync($"/api/tags/{tag1.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_NonexistentTag_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/tags/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesTagAndReturns204()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag = await SeedTagAsync("Doomed");
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/tags/{tag.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        (await db.Tags.FindAsync(tag.Id)).Should().BeNull();
    }

    private async Task<Tag> SeedTagAsync(string name)
    {
        await using var db = Factory.CreateDbContext();
        var tag = new Tag { Name = name };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }
}
