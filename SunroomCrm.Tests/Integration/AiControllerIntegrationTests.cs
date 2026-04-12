using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Integration tests for /api/ai endpoints. Uses <see cref="TestAiService"/>
/// (registered in <see cref="CustomWebApplicationFactory"/>) to keep the AI
/// responses deterministic and avoid Ollama from running in tests.
/// </summary>
public class AiControllerIntegrationTests : IntegrationTestBase
{
    public AiControllerIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ---- Authorization ----

    [Fact]
    public async Task Summarize_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/ai/summarize", new SummarizeRequest { Text = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SmartSearch_WithoutAuth_Returns401()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/ai/search", new SmartSearchRequest { Query = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- POST /api/ai/summarize ----

    [Fact]
    public async Task Summarize_ReturnsTestSummary()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);
        var request = new SummarizeRequest { Text = "Some long text to summarize." };

        var response = await client.PostAsJsonAsync("/api/ai/summarize", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<SummarizeResponse>(response);
        dto!.Summary.Should().Be(TestAiService.SummaryText);
    }

    // ---- POST /api/ai/deal-insights/{dealId} ----

    [Fact]
    public async Task DealInsights_NonexistentDeal_Returns404()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/api/ai/deal-insights/99999", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DealInsights_PersistsInsightAndReturnsIt()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        var contact = await SeedContactAsync(user.Id);
        var deal = await SeedDealAsync(user.Id, contact.Id, "Insight Deal");
        var client = CreateAuthenticatedClient(user);

        var response = await client.PostAsync($"/api/ai/deal-insights/{deal.Id}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<DealInsightDto>(response);
        dto!.Insight.Should().Be(TestAiService.InsightText);
        dto.Id.Should().BeGreaterThan(0);

        await using var db = Factory.CreateDbContext();
        var persisted = await db.AiInsights.FindAsync(dto.Id);
        persisted.Should().NotBeNull();
        persisted!.DealId.Should().Be(deal.Id);
        persisted.Insight.Should().Be(TestAiService.InsightText);
    }

    // ---- POST /api/ai/search ----

    [Fact]
    public async Task SmartSearch_ReturnsResponseWithUserScopedData()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync();
        await SeedContactAsync(user.Id, first: "Search", last: "Target");
        var client = CreateAuthenticatedClient(user);
        var request = new SmartSearchRequest { Query = "search" };

        var response = await client.PostAsJsonAsync("/api/ai/search", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadJsonAsync<SmartSearchResponse>(response);
        dto!.Interpretation.Should().Be(TestAiService.InterpretationText);
        dto.Contacts.Should().HaveCount(1);
        dto.Contacts[0].FirstName.Should().Be("Search");
    }

    [Fact]
    public async Task SmartSearch_OnlyIncludesCurrentUserContacts()
    {
        await ResetDatabaseAsync();
        var user1 = await SeedUserAsync(email: "u1@x.com");
        var user2 = await SeedUserAsync(email: "u2@x.com");
        await SeedContactAsync(user1.Id, first: "Mine", last: "One");
        await SeedContactAsync(user2.Id, first: "Theirs", last: "Two");
        var client = CreateAuthenticatedClient(user1);

        var response = await client.PostAsJsonAsync("/api/ai/search", new SmartSearchRequest { Query = "any" });

        var dto = await ReadJsonAsync<SmartSearchResponse>(response);
        dto!.Contacts.Should().HaveCount(1);
        dto.Contacts[0].FirstName.Should().Be("Mine");
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

    private async Task<Deal> SeedDealAsync(int userId, int contactId, string title)
    {
        await using var db = Factory.CreateDbContext();
        var deal = new Deal
        {
            UserId = userId,
            ContactId = contactId,
            Title = title,
            Value = 1000m,
            Stage = DealStage.Qualified
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }
}
