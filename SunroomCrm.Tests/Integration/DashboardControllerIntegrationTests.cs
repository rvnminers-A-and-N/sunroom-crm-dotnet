using System.Net;
using SunroomCrm.Core.DTOs.Dashboard;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/dashboard. Verifies the aggregate counts,
/// pipeline value calculation (excludes Won/Lost), won revenue, and
/// recent-activities feed for the authenticated user only.
/// </summary>
public class DashboardControllerIntegrationTests : IntegrationTestBase
{
    public DashboardControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ReturnsAggregatedStatsForCurrentUser()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var company = await SeedCompanyAsync(user.Id, "Acme");
        var contact = await SeedContactAsync(user.Id, company.Id);
        await SeedDealAsync(user.Id, contact.Id, "Open1", 1000m, DealStage.Qualified);
        await SeedDealAsync(user.Id, contact.Id, "Open2", 2000m, DealStage.Negotiation);
        await SeedDealAsync(user.Id, contact.Id, "Won1", 5000m, DealStage.Won);
        await SeedDealAsync(user.Id, contact.Id, "Lost1", 100m, DealStage.Lost);
        await SeedActivityAsync(user.Id, contact.Id, "Recent call");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await ReadJsonAsync<DashboardDto>(response);
        dashboard.Should().NotBeNull();
        dashboard!.TotalContacts.Should().Be(1);
        dashboard.TotalCompanies.Should().Be(1);
        dashboard.TotalDeals.Should().Be(4);
        dashboard.TotalPipelineValue.Should().Be(3000m);
        dashboard.WonRevenue.Should().Be(5000m);
        dashboard.RecentActivities.Should().NotBeEmpty();
        dashboard.RecentActivities[0].Subject.Should().Be("Recent call");
    }

    [Fact]
    public async Task Get_DealsByStage_IncludesAllStagesWithData()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedDealAsync(user.Id, contact.Id, "L1", 100m, DealStage.Lead);
        await SeedDealAsync(user.Id, contact.Id, "L2", 200m, DealStage.Lead);
        await SeedDealAsync(user.Id, contact.Id, "Q1", 500m, DealStage.Qualified);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/dashboard");

        var dashboard = await ReadJsonAsync<DashboardDto>(response);
        var lead = dashboard!.DealsByStage.FirstOrDefault(s => s.Stage == "Lead");
        lead.Should().NotBeNull();
        lead!.Count.Should().Be(2);
        lead.TotalValue.Should().Be(300m);

        var qualified = dashboard.DealsByStage.FirstOrDefault(s => s.Stage == "Qualified");
        qualified.Should().NotBeNull();
        qualified!.Count.Should().Be(1);
        qualified.TotalValue.Should().Be(500m);
    }

    [Fact]
    public async Task Get_OnlyAggregatesCurrentUserData()
    {
        await ResetDatabaseAsync();
        var user1 = await SeedUserAsync(email: "u1@x.com");
        var user2 = await SeedUserAsync(email: "u2@x.com");
        var contact1 = await SeedContactAsync(user1.Id);
        var contact2 = await SeedContactAsync(user2.Id);
        await SeedCompanyAsync(user1.Id, "User1Co");
        await SeedCompanyAsync(user2.Id, "User2Co");
        await SeedDealAsync(user1.Id, contact1.Id, "U1 Deal", 100m, DealStage.Qualified);
        await SeedDealAsync(user2.Id, contact2.Id, "U2 Deal", 999m, DealStage.Qualified);
        var client = CreateAuthenticatedClient(user1);

        var response = await client.GetAsync("/api/dashboard");

        var dashboard = await ReadJsonAsync<DashboardDto>(response);
        dashboard!.TotalContacts.Should().Be(1);
        dashboard.TotalCompanies.Should().Be(1);
        dashboard.TotalDeals.Should().Be(1);
        dashboard.TotalPipelineValue.Should().Be(100m);
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

    private async Task<Contact> SeedContactAsync(int userId, int? companyId = null)
    {
        await using var db = Factory.CreateDbContext();
        var contact = new Contact
        {
            UserId = userId,
            FirstName = "Test",
            LastName = "Contact",
            Email = $"{Guid.NewGuid():N}@example.com",
            CompanyId = companyId
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    private async Task<Deal> SeedDealAsync(int userId, int contactId, string title, decimal value, DealStage stage)
    {
        await using var db = Factory.CreateDbContext();
        var deal = new Deal
        {
            UserId = userId,
            ContactId = contactId,
            Title = title,
            Value = value,
            Stage = stage,
            ClosedAt = stage is DealStage.Won or DealStage.Lost ? DateTime.UtcNow : null
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
