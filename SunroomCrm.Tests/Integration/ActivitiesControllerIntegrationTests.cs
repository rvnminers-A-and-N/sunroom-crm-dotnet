using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/activities endpoints. Covers CRUD,
/// type filtering, contact/deal filters, and the type-validation
/// 400 path on Create.
/// </summary>
public class ActivitiesControllerIntegrationTests : IntegrationTestBase
{
    public ActivitiesControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/activities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET /api/activities ----

    [Fact]
    public async Task GetAll_Authenticated_ReturnsPaginatedList()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedActivityAsync(user.Id, contact.Id, ActivityType.Call, "Call 1");
        await SeedActivityAsync(user.Id, contact.Id, ActivityType.Email, "Email 1");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync<PaginatedResponse<ActivityDto>>(response);
        page!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_WithTypeFilter_OnlyReturnsMatchingActivities()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedActivityAsync(user.Id, contact.Id, ActivityType.Call, "Call");
        await SeedActivityAsync(user.Id, contact.Id, ActivityType.Email, "Email");
        await SeedActivityAsync(user.Id, contact.Id, ActivityType.Note, "Note");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/activities?type=Call");

        var page = await ReadJsonAsync<PaginatedResponse<ActivityDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].Type.Should().Be("Call");
    }

    [Fact]
    public async Task GetAll_WithContactFilter_OnlyReturnsActivitiesForContact()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact1 = await SeedContactAsync(user.Id, first: "One");
        var contact2 = await SeedContactAsync(user.Id, first: "Two");
        await SeedActivityAsync(user.Id, contact1.Id, ActivityType.Call, "For one");
        await SeedActivityAsync(user.Id, contact2.Id, ActivityType.Call, "For two");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/activities?contactId={contact1.Id}");

        var page = await ReadJsonAsync<PaginatedResponse<ActivityDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].Subject.Should().Be("For one");
    }

    // ---- GET /api/activities/{id} ----

    [Fact]
    public async Task GetById_NonexistentActivity_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/activities/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsActivity()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var activity = await SeedActivityAsync(user.Id, contact.Id, ActivityType.Meeting, "Strategy");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/activities/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<ActivityDto>(response);
        dto!.Subject.Should().Be("Strategy");
        dto.Type.Should().Be("Meeting");
    }

    // ---- POST /api/activities ----

    [Fact]
    public async Task Create_InvalidType_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateActivityRequest
        {
            Type = "NotARealType",
            Subject = "Bad"
        };

        var response = await client.PostAsJsonAsync("/api/activities", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PersistsActivityAndReturns201()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var client = CreateAuthenticatedClient(user);
        var request = new CreateActivityRequest
        {
            Type = "Call",
            Subject = "Discovery",
            Body = "First call",
            ContactId = contact.Id
        };

        var response = await client.PostAsJsonAsync("/api/activities", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<ActivityDto>(response);
        created!.Subject.Should().Be("Discovery");
        created.Type.Should().Be("Call");

        await using var db = Factory.CreateDbContext();
        var persisted = await db.Activities.FindAsync(created.Id);
        persisted!.UserId.Should().Be(user.Id);
        persisted.Type.Should().Be(ActivityType.Call);
    }

    [Fact]
    public async Task Create_TypeIsCaseInsensitive()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateActivityRequest
        {
            Type = "email",
            Subject = "Lowercase"
        };

        var response = await client.PostAsJsonAsync("/api/activities", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<ActivityDto>(response);
        created!.Type.Should().Be("Email");
    }

    // ---- PUT /api/activities/{id} ----

    [Fact]
    public async Task Update_NonexistentActivity_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateActivityRequest { Type = "Call", Subject = "X" };

        var response = await client.PutAsJsonAsync("/api/activities/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_PersistsAllFields()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var activity = await SeedActivityAsync(user.Id, contact.Id, ActivityType.Note, "Old");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateActivityRequest
        {
            Type = "Meeting",
            Subject = "Updated",
            Body = "New body",
            ContactId = contact.Id
        };

        var response = await client.PutAsJsonAsync($"/api/activities/{activity.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Activities.FindAsync(activity.Id);
        persisted!.Type.Should().Be(ActivityType.Meeting);
        persisted.Subject.Should().Be("Updated");
        persisted.Body.Should().Be("New body");
    }

    // ---- DELETE /api/activities/{id} ----

    [Fact]
    public async Task Delete_NonexistentActivity_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/activities/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesActivityAndReturns204()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var activity = await SeedActivityAsync(user.Id, contact.Id, ActivityType.Task, "Doomed");
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/activities/{activity.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        (await db.Activities.FindAsync(activity.Id)).Should().BeNull();
    }

    // ---- Helpers ----

    private async Task<Contact> SeedContactAsync(int userId, string first = "Test", string last = "Contact")
    {
        await using var db = Factory.CreateDbContext();
        var contact = new Contact
        {
            UserId = userId,
            FirstName = first,
            LastName = last,
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    private async Task<Activity> SeedActivityAsync(int userId, int? contactId, ActivityType type, string subject)
    {
        await using var db = Factory.CreateDbContext();
        var activity = new Activity
        {
            UserId = userId,
            ContactId = contactId,
            Type = type,
            Subject = subject,
            OccurredAt = DateTime.UtcNow
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return activity;
    }
}
