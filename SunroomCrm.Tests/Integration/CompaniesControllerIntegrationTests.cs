using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/companies endpoints. Exercises the full HTTP
/// pipeline (auth + JSON serialization + EF InMemory repository) for the
/// Company CRUD surface, search/pagination behavior, and the with-details GET.
/// </summary>
public class CompaniesControllerIntegrationTests : IntegrationTestBase
{
    public CompaniesControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithExpiredToken_Returns401()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateClientWithExpiredToken(user);

        var response = await client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET /api/companies ----

    [Fact]
    public async Task GetAll_Authenticated_ReturnsPaginatedList()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedCompanyAsync(user.Id, name: "Acme Inc");
        await SeedCompanyAsync(user.Id, name: "Globex");
        await SeedCompanyAsync(user.Id, name: "Initech");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(response);
        page.Should().NotBeNull();
        page!.Data.Should().HaveCount(3);
        page.Meta.Total.Should().Be(3);
        page.Meta.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_WithSearchFilter_ReturnsMatchingCompanies()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedCompanyAsync(user.Id, name: "Acme Inc");
        await SeedCompanyAsync(user.Id, name: "Globex");
        await SeedCompanyAsync(user.Id, name: "Acme Subsidiary");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/companies?search=Acme");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(response);
        page!.Data.Should().HaveCount(2);
        page.Data.Should().OnlyContain(c => c.Name.Contains("Acme"));
    }

    [Fact]
    public async Task GetAll_OnlyReturnsCompaniesForCurrentUser()
    {
        await ResetDatabaseAsync();
        var user1 = await SeedUserAsync(email: "u1@x.com");
        var user2 = await SeedUserAsync(email: "u2@x.com");
        await SeedCompanyAsync(user1.Id, name: "User1Co");
        await SeedCompanyAsync(user2.Id, name: "User2Co");
        var client = CreateAuthenticatedClient(user1);

        var response = await client.GetAsync("/api/companies");

        var page = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].Name.Should().Be("User1Co");
    }

    [Fact]
    public async Task GetAll_RespectsPagination()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await SeedCompanyAsync(user.Id, name: $"Co{i}");
        }
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/companies?page=1&perPage=2");

        var page = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(response);
        page!.Data.Should().HaveCount(2);
        page.Meta.Total.Should().Be(5);
        page.Meta.PerPage.Should().Be(2);
        page.Meta.LastPage.Should().Be(3);
    }

    // ---- GET /api/companies/{id} ----

    [Fact]
    public async Task GetById_NonexistentCompany_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/companies/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsCompanyWithContactsAndDeals()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, name: "BigCo");
        var contact = await SeedContactAsync(user.Id, "Jane", "Doe", company.Id);
        await SeedDealAsync(user.Id, contact.Id, company.Id, "Big Deal", 50000m);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/companies/{company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync<CompanyDetailDto>(response);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be("BigCo");
        detail.Contacts.Should().HaveCount(1);
        detail.Contacts[0].FirstName.Should().Be("Jane");
        detail.Deals.Should().HaveCount(1);
        detail.Deals[0].Title.Should().Be("Big Deal");
    }

    // ---- POST /api/companies ----

    [Fact]
    public async Task Create_PersistsCompanyAndReturns201()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateCompanyRequest
        {
            Name = "New Company",
            Industry = "Software",
            Website = "https://example.com",
            City = "Austin",
            State = "TX"
        };

        var response = await client.PostAsJsonAsync("/api/companies", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<CompanyDto>(response);
        created!.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be("New Company");

        await using var db = Factory.CreateDbContext();
        var persisted = await db.Companies.FindAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(user.Id);
        persisted.Industry.Should().Be("Software");
    }

    [Fact]
    public async Task Create_MissingRequiredName_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new { Industry = "X" }; // no Name

        var response = await client.PostAsJsonAsync("/api/companies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- PUT /api/companies/{id} ----

    [Fact]
    public async Task Update_NonexistentCompany_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateCompanyRequest { Name = "Whatever" };

        var response = await client.PutAsJsonAsync("/api/companies/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_PersistsAllFields()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, name: "Old Name");
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateCompanyRequest
        {
            Name = "New Name",
            Industry = "Hardware",
            Website = "https://new.com",
            Phone = "555-1234",
            Address = "123 Main",
            City = "Dallas",
            State = "TX",
            Zip = "75001",
            Notes = "Updated notes"
        };

        var response = await client.PutAsJsonAsync($"/api/companies/{company.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Companies.FindAsync(company.Id);
        persisted!.Name.Should().Be("New Name");
        persisted.Industry.Should().Be("Hardware");
        persisted.City.Should().Be("Dallas");
        persisted.Notes.Should().Be("Updated notes");
    }

    // ---- DELETE /api/companies/{id} ----

    [Fact]
    public async Task Delete_NonexistentCompany_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/companies/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesCompanyAndReturns204()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, name: "Doomed");
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/companies/{company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        (await db.Companies.FindAsync(company.Id)).Should().BeNull();
    }

    // ---- Helpers ----

    private async Task<Company> SeedCompanyAsync(int userId, string name)
    {
        await using var db = Factory.CreateDbContext();
        var company = new Company { UserId = userId, Name = name };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

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

    private async Task<Deal> SeedDealAsync(int userId, int contactId, int? companyId, string title, decimal value)
    {
        await using var db = Factory.CreateDbContext();
        var deal = new Deal
        {
            UserId = userId,
            ContactId = contactId,
            CompanyId = companyId,
            Title = title,
            Value = value,
            Stage = DealStage.Qualified
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }
}
