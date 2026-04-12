using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/deals endpoints. Covers CRUD, the
/// stage-based pipeline aggregation, and the Won/Lost ClosedAt
/// transition logic enforced in the Update action.
/// </summary>
public class DealsControllerIntegrationTests : IntegrationTestBase
{
    public DealsControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/deals");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pipeline_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/deals/pipeline");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- GET /api/deals ----

    [Fact]
    public async Task GetAll_Authenticated_ReturnsPaginatedList()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedDealAsync(user.Id, contact.Id, "Deal 1", 100m);
        await SeedDealAsync(user.Id, contact.Id, "Deal 2", 200m);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/deals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync<PaginatedResponse<DealDto>>(response);
        page!.Data.Should().HaveCount(2);
        page.Meta.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_WithStageFilter_OnlyReturnsMatchingDeals()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedDealAsync(user.Id, contact.Id, "Lead Deal", 100m, DealStage.Lead);
        await SeedDealAsync(user.Id, contact.Id, "Won Deal", 500m, DealStage.Won);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/deals?stage=Won");

        var page = await ReadJsonAsync<PaginatedResponse<DealDto>>(response);
        page!.Data.Should().HaveCount(1);
        page.Data[0].Title.Should().Be("Won Deal");
    }

    // ---- GET /api/deals/{id} ----

    [Fact]
    public async Task GetById_NonexistentDeal_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/deals/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsDealWithDetails()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id, "Jane", "Doe");
        var deal = await SeedDealAsync(user.Id, contact.Id, "Big Deal", 50000m, DealStage.Negotiation);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/deals/{deal.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync<DealDetailDto>(response);
        detail!.Title.Should().Be("Big Deal");
        detail.Value.Should().Be(50000m);
        detail.Stage.Should().Be("Negotiation");
        detail.ContactName.Should().Be("Jane Doe");
    }

    // ---- POST /api/deals ----

    [Fact]
    public async Task Create_NonexistentContact_Returns400()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new CreateDealRequest
        {
            Title = "X",
            Value = 1m,
            ContactId = 99999,
            Stage = "Lead"
        };

        var response = await client.PostAsJsonAsync("/api/deals", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PersistsDealAndReturns201()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var client = CreateAuthenticatedClient(user);
        var request = new CreateDealRequest
        {
            Title = "New Deal",
            Value = 12345m,
            ContactId = contact.Id,
            Stage = "Qualified"
        };

        var response = await client.PostAsJsonAsync("/api/deals", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<DealDto>(response);
        created!.Title.Should().Be("New Deal");
        created.Stage.Should().Be("Qualified");

        await using var db = Factory.CreateDbContext();
        var persisted = await db.Deals.FindAsync(created.Id);
        persisted!.UserId.Should().Be(user.Id);
        persisted.Stage.Should().Be(DealStage.Qualified);
    }

    [Fact]
    public async Task Create_InvalidStage_DefaultsToLead()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var client = CreateAuthenticatedClient(user);
        var request = new CreateDealRequest
        {
            Title = "Stageless",
            Value = 1m,
            ContactId = contact.Id,
            Stage = "NotARealStage"
        };

        var response = await client.PostAsJsonAsync("/api/deals", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync<DealDto>(response);
        created!.Stage.Should().Be("Lead");
    }

    // ---- PUT /api/deals/{id} ----

    [Fact]
    public async Task Update_NonexistentDeal_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateDealRequest { Title = "X", Value = 1m, ContactId = 1 };

        var response = await client.PutAsJsonAsync("/api/deals/99999", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_TransitionToWon_SetsClosedAt()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var deal = await SeedDealAsync(user.Id, contact.Id, "Closing", 1000m, DealStage.Negotiation);
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateDealRequest
        {
            Title = "Closing",
            Value = 1000m,
            ContactId = contact.Id,
            Stage = "Won"
        };

        var response = await client.PutAsJsonAsync($"/api/deals/{deal.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Deals.FindAsync(deal.Id);
        persisted!.Stage.Should().Be(DealStage.Won);
        persisted.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_TransitionFromWonToOpenStage_ClearsClosedAt()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var deal = await SeedDealAsync(user.Id, contact.Id, "Reopen", 100m, DealStage.Won);
        await using (var db = Factory.CreateDbContext())
        {
            var d = await db.Deals.FindAsync(deal.Id);
            d!.ClosedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var client = CreateAuthenticatedClient(user);
        var request = new UpdateDealRequest
        {
            Title = "Reopen",
            Value = 100m,
            ContactId = contact.Id,
            Stage = "Negotiation"
        };

        var response = await client.PutAsJsonAsync($"/api/deals/{deal.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db2 = Factory.CreateDbContext();
        var persisted = await db2.Deals.FindAsync(deal.Id);
        persisted!.Stage.Should().Be(DealStage.Negotiation);
        persisted.ClosedAt.Should().BeNull();
    }

    // ---- DELETE /api/deals/{id} ----

    [Fact]
    public async Task Delete_NonexistentDeal_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync("/api/deals/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesDealAndReturns204()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var deal = await SeedDealAsync(user.Id, contact.Id, "Doomed", 1m);
        var client = CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/deals/{deal.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        (await db.Deals.FindAsync(deal.Id)).Should().BeNull();
    }

    // ---- GET /api/deals/pipeline ----

    [Fact]
    public async Task Pipeline_ReturnsAllStagesWithCountsAndTotals()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        await SeedDealAsync(user.Id, contact.Id, "L1", 100m, DealStage.Lead);
        await SeedDealAsync(user.Id, contact.Id, "L2", 200m, DealStage.Lead);
        await SeedDealAsync(user.Id, contact.Id, "Q1", 500m, DealStage.Qualified);
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/deals/pipeline");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pipeline = await ReadJsonAsync<PipelineDto>(response);
        pipeline!.Stages.Should().HaveCount(Enum.GetValues<DealStage>().Length);

        var lead = pipeline.Stages.First(s => s.Stage == "Lead");
        lead.Count.Should().Be(2);
        lead.TotalValue.Should().Be(300m);

        var qualified = pipeline.Stages.First(s => s.Stage == "Qualified");
        qualified.Count.Should().Be(1);
        qualified.TotalValue.Should().Be(500m);
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

    private async Task<Deal> SeedDealAsync(int userId, int contactId, string title, decimal value, DealStage stage = DealStage.Lead)
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
}
