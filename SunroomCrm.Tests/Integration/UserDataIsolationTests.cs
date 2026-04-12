using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Cross-cutting integration tests focused on per-user data isolation. The
/// API exposes user-scoped repositories for companies, contacts, deals, and
/// activities — these tests verify that one authenticated user cannot see
/// rows owned by another user via any list endpoint.
///
/// They complement the per-controller integration tests (which mostly cover
/// happy paths and authorization) by exercising the user filter explicitly
/// in a multi-tenant scenario inside a single test class.
/// </summary>
public class UserDataIsolationTests : IntegrationTestBase
{
    public UserDataIsolationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Companies_AreScopedPerUser()
    {
        await ResetDatabaseAsync();
        var (alice, bob) = await SeedTwoUsersAsync();
        await SeedCompanyAsync(alice.Id, "AliceCo");
        await SeedCompanyAsync(bob.Id, "BobCo");

        var aliceClient = CreateAuthenticatedClient(alice);
        var bobClient = CreateAuthenticatedClient(bob);

        var alicePage = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(
            await aliceClient.GetAsync("/api/companies"));
        var bobPage = await ReadJsonAsync<PaginatedResponse<CompanyDto>>(
            await bobClient.GetAsync("/api/companies"));

        alicePage!.Data.Should().ContainSingle().Which.Name.Should().Be("AliceCo");
        bobPage!.Data.Should().ContainSingle().Which.Name.Should().Be("BobCo");
    }

    [Fact]
    public async Task Contacts_AreScopedPerUser()
    {
        await ResetDatabaseAsync();
        var (alice, bob) = await SeedTwoUsersAsync();
        await SeedContactAsync(alice.Id, "Alice");
        await SeedContactAsync(bob.Id, "Bob");

        var aliceClient = CreateAuthenticatedClient(alice);
        var bobClient = CreateAuthenticatedClient(bob);

        var alicePage = await ReadJsonAsync<PaginatedResponse<ContactDto>>(
            await aliceClient.GetAsync("/api/contacts"));
        var bobPage = await ReadJsonAsync<PaginatedResponse<ContactDto>>(
            await bobClient.GetAsync("/api/contacts"));

        alicePage!.Data.Should().ContainSingle().Which.FirstName.Should().Be("Alice");
        bobPage!.Data.Should().ContainSingle().Which.FirstName.Should().Be("Bob");
    }

    [Fact]
    public async Task Deals_AreScopedPerUser()
    {
        await ResetDatabaseAsync();
        var (alice, bob) = await SeedTwoUsersAsync();
        var aliceContact = await SeedContactAsync(alice.Id, "Alice");
        var bobContact = await SeedContactAsync(bob.Id, "Bob");
        await SeedDealAsync(alice.Id, aliceContact.Id, "Alice Deal");
        await SeedDealAsync(bob.Id, bobContact.Id, "Bob Deal");

        var aliceClient = CreateAuthenticatedClient(alice);
        var bobClient = CreateAuthenticatedClient(bob);

        var alicePage = await ReadJsonAsync<PaginatedResponse<DealDto>>(
            await aliceClient.GetAsync("/api/deals"));
        var bobPage = await ReadJsonAsync<PaginatedResponse<DealDto>>(
            await bobClient.GetAsync("/api/deals"));

        alicePage!.Data.Should().ContainSingle().Which.Title.Should().Be("Alice Deal");
        bobPage!.Data.Should().ContainSingle().Which.Title.Should().Be("Bob Deal");
    }

    [Fact]
    public async Task Activities_AreScopedPerUser()
    {
        await ResetDatabaseAsync();
        var (alice, bob) = await SeedTwoUsersAsync();
        var aliceContact = await SeedContactAsync(alice.Id, "Alice");
        var bobContact = await SeedContactAsync(bob.Id, "Bob");
        await SeedActivityAsync(alice.Id, aliceContact.Id, "Alice Activity");
        await SeedActivityAsync(bob.Id, bobContact.Id, "Bob Activity");

        var aliceClient = CreateAuthenticatedClient(alice);
        var bobClient = CreateAuthenticatedClient(bob);

        var alicePage = await ReadJsonAsync<PaginatedResponse<ActivityDto>>(
            await aliceClient.GetAsync("/api/activities"));
        var bobPage = await ReadJsonAsync<PaginatedResponse<ActivityDto>>(
            await bobClient.GetAsync("/api/activities"));

        alicePage!.Data.Should().ContainSingle().Which.Subject.Should().Be("Alice Activity");
        bobPage!.Data.Should().ContainSingle().Which.Subject.Should().Be("Bob Activity");
    }

    [Fact]
    public async Task DealsPipeline_AggregatesPerUser()
    {
        await ResetDatabaseAsync();
        var (alice, bob) = await SeedTwoUsersAsync();
        var aliceContact = await SeedContactAsync(alice.Id, "Alice");
        var bobContact = await SeedContactAsync(bob.Id, "Bob");
        await SeedDealAsync(alice.Id, aliceContact.Id, "Alice Lead", 100m, DealStage.Lead);
        await SeedDealAsync(bob.Id, bobContact.Id, "Bob Lead", 999m, DealStage.Lead);

        var aliceClient = CreateAuthenticatedClient(alice);
        var pipeline = await ReadJsonAsync<PipelineDto>(
            await aliceClient.GetAsync("/api/deals/pipeline"));

        var lead = pipeline!.Stages.First(s => s.Stage == "Lead");
        lead.Count.Should().Be(1);
        lead.TotalValue.Should().Be(100m);
    }

    // ---- Helpers ----

    private async Task<(User alice, User bob)> SeedTwoUsersAsync()
    {
        var alice = await SeedUserAsync(name: "Alice", email: "alice@x.com");
        var bob = await SeedUserAsync(name: "Bob", email: "bob@x.com");
        return (alice, bob);
    }

    private async Task<Company> SeedCompanyAsync(int userId, string name)
    {
        await using var db = Factory.CreateDbContext();
        var company = new Company { UserId = userId, Name = name };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private async Task<Contact> SeedContactAsync(int userId, string firstName)
    {
        await using var db = Factory.CreateDbContext();
        var contact = new Contact
        {
            UserId = userId,
            FirstName = firstName,
            LastName = "Person",
            Email = $"{firstName.ToLower()}_{Guid.NewGuid():N}@example.com"
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    private async Task<Deal> SeedDealAsync(int userId, int contactId, string title, decimal value = 1000m, DealStage stage = DealStage.Qualified)
    {
        await using var db = Factory.CreateDbContext();
        var deal = new Deal
        {
            UserId = userId,
            ContactId = contactId,
            Title = title,
            Value = value,
            Stage = stage
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }

    private async Task<Activity> SeedActivityAsync(int userId, int contactId, string subject)
    {
        await using var db = Factory.CreateDbContext();
        var activity = new Activity
        {
            UserId = userId,
            ContactId = contactId,
            Type = ActivityType.Call,
            Subject = subject,
            OccurredAt = DateTime.UtcNow
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return activity;
    }
}
