using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/contacts endpoints. Covers CRUD,
/// filter/pagination behavior, the with-details GET, and the
/// POST /api/contacts/{id}/tags sync endpoint that wires Contacts
/// to Tags via the join entity.
/// </summary>
public class ContactsControllerIntegrationTests : IntegrationTestBase
{
    public ContactsControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithMalformedToken_Returns401()
    {
        var client = CreateClientWithMalformedToken();

        var response = await client.GetAsync("/api/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET /api/contacts ----

    [Fact]
    public async Task GetAll_Authenticated_ReturnsPaginatedList()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedContactAsync(user.Id, "Alice", "Anderson");
        await SeedContactAsync(user.Id, "Bob", "Brown");
        await SeedContactAsync(user.Id, "Carol", "Clark");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync<PaginatedResponse<ContactDto>>(response);
        page!.Data.Should().HaveCount(3);
        page.Meta.Total.Should().Be(3);
    }

    [Fact]
    public async Task GetAll_WithSearchFilter_ReturnsMatchingContacts()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedContactAsync(user.Id, "Alice", "Anderson");
        await SeedContactAsync(user.Id, "Bob", "Brown");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/contacts?search=Alice");

        var page = await ReadJsonAsync<PaginatedResponse<ContactDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetAll_WithCompanyFilter_OnlyReturnsContactsAtCompany()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, "FilterCo");
        await SeedContactAsync(user.Id, "Alice", "InCompany", company.Id);
        await SeedContactAsync(user.Id, "Bob", "NoCompany");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/contacts?companyId={company.Id}");

        var page = await ReadJsonAsync<PaginatedResponse<ContactDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetAll_OnlyReturnsContactsForCurrentUser()
    {
        await ResetDatabaseAsync();
        var user1 = await SeedUserAsync(email: "u1@x.com");
        var user2 = await SeedUserAsync(email: "u2@x.com");
        await SeedContactAsync(user1.Id, "Mine", "Mine");
        await SeedContactAsync(user2.Id, "Theirs", "Theirs");
        var client = CreateAuthenticatedClient(user1);

        var response = await client.GetAsync("/api/contacts");

        var page = await ReadJsonAsync<PaginatedResponse<ContactDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].FirstName.Should().Be("Mine");
    }

    // ---- GET /api/contacts/{id} ----

    [Fact]
    public async Task GetById_NonexistentContact_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/contacts/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsContactWithCompanyAndTags()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, "DetailCo");
        var tag = await SeedTagAsync("VIP");
        var contact = await SeedContactAsync(user.Id, "Detail", "Person", company.Id);
        await AttachTagAsync(contact.Id, tag.Id);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/contacts/{contact.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync<ContactDetailDto>(response);
        detail!.FirstName.Should().Be("Detail");
        detail.Company.Should().NotBeNull();
        detail.Company!.Name.Should().Be("DetailCo");
        detail.Tags.Should().HaveCount(1);
        detail.Tags[0].Name.Should().Be("VIP");
    }

    // ---- POST /api/contacts ----

    [Fact]
    public async Task Create_PersistsContactAndReturns201()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateContactRequest
        {
            FirstName = "New",
            LastName = "Contact",
            Email = "new@example.com",
            Phone = "555-1111",
            Title = "Manager"
        };

        var response = await client.PostAsJsonAsync("/api/contacts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<ContactDto>(response);
        created!.Id.Should().BeGreaterThan(0);
        created.FirstName.Should().Be("New");

        await using var db = Factory.CreateDbContext();
        var persisted = await db.Contacts.FindAsync(created.Id);
        persisted!.UserId.Should().Be(user.Id);
        persisted.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task Create_WithTagIds_AssociatesTags()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag1 = await SeedTagAsync("HotLead");
        var tag2 = await SeedTagAsync("Enterprise");
        var client = CreateAuthenticatedClient(user);
        var request = new CreateContactRequest
        {
            FirstName = "Tagged",
            LastName = "Person",
            TagIds = new List<int> { tag1.Id, tag2.Id }
        };

        var response = await client.PostAsJsonAsync("/api/contacts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<ContactDto>(response);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Contacts
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == created!.Id);
        persisted.Tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new { FirstName = "OnlyFirst" }; // missing LastName

        var response = await client.PostAsJsonAsync("/api/contacts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- PUT /api/contacts/{id} ----

    [Fact]
    public async Task Update_NonexistentContact_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateContactRequest { FirstName = "X", LastName = "Y" };

        var response = await client.PutAsJsonAsync("/api/contacts/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_PersistsAllFields()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id, "Old", "Name");
        var company = await SeedCompanyAsync(user.Id, "NewCo");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateContactRequest
        {
            FirstName = "Updated",
            LastName = "Surname",
            Email = "updated@example.com",
            Phone = "555-9999",
            Title = "VP",
            Notes = "New notes",
            CompanyId = company.Id
        };

        var response = await client.PutAsJsonAsync($"/api/contacts/{contact.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Contacts.FindAsync(contact.Id);
        persisted!.FirstName.Should().Be("Updated");
        persisted.LastName.Should().Be("Surname");
        persisted.Email.Should().Be("updated@example.com");
        persisted.CompanyId.Should().Be(company.Id);
    }

    // ---- DELETE /api/contacts/{id} ----

    [Fact]
    public async Task Delete_NonexistentContact_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/contacts/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesContactAndReturns204()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id, "Doomed", "Person");
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/contacts/{contact.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        (await db.Contacts.FindAsync(contact.Id)).Should().BeNull();
    }

    // ---- POST /api/contacts/{id}/tags ----

    [Fact]
    public async Task SyncTags_NonexistentContact_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new SyncTagsRequest { TagIds = new List<int> { 1 } };

        var response = await client.PostAsJsonAsync("/api/contacts/99999/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SyncTags_ReplacesContactTags()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag1 = await SeedTagAsync("Initial");
        var tag2 = await SeedTagAsync("New1");
        var tag3 = await SeedTagAsync("New2");
        var contact = await SeedContactAsync(user.Id, "Sync", "Test");
        await AttachTagAsync(contact.Id, tag1.Id);
        var client = CreateAuthenticatedClient(user);
        var request = new SyncTagsRequest { TagIds = new List<int> { tag2.Id, tag3.Id } };

        var response = await client.PostAsJsonAsync($"/api/contacts/{contact.Id}/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Contacts
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == contact.Id);
        persisted.Tags.Should().HaveCount(2);
        persisted.Tags.Should().NotContain(t => t.Id == tag1.Id);
        persisted.Tags.Should().Contain(t => t.Id == tag2.Id);
        persisted.Tags.Should().Contain(t => t.Id == tag3.Id);
    }

    [Fact]
    public async Task SyncTags_WithEmptyList_ClearsAllTags()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var tag = await SeedTagAsync("ToBeRemoved");
        var contact = await SeedContactAsync(user.Id, "Empty", "Tags");
        await AttachTagAsync(contact.Id, tag.Id);
        var client = CreateAuthenticatedClient(user);
        var request = new SyncTagsRequest { TagIds = new List<int>() };

        var response = await client.PostAsJsonAsync($"/api/contacts/{contact.Id}/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Contacts
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == contact.Id);
        persisted.Tags.Should().BeEmpty();
    }

    // ---- Helpers ----

    private async Task<Contact> SeedContactAsync(int userId, string first, string last, int? companyId = null)
    {
        await using var db = Factory.CreateDbContext();
        var contact = new Contact
        {
            UserId = userId,
            FirstName = first,
            LastName = last,
            Email = $"{first}.{last}@example.com".ToLower(),
            CompanyId = companyId
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    private async Task<Company> SeedCompanyAsync(int userId, string name)
    {
        await using var db = Factory.CreateDbContext();
        var company = new Company { UserId = userId, Name = name };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private async Task<Tag> SeedTagAsync(string name)
    {
        await using var db = Factory.CreateDbContext();
        var tag = new Tag { Name = name };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    private async Task AttachTagAsync(int contactId, int tagId)
    {
        await using var db = Factory.CreateDbContext();
        var contact = await db.Contacts.Include(c => c.Tags).FirstAsync(c => c.Id == contactId);
        var tag = await db.Tags.FindAsync(tagId);
        contact.Tags.Add(tag!);
        await db.SaveChangesAsync();
    }
}
