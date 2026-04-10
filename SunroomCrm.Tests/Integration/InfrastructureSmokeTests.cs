using System.Net;
using System.Net.Http.Json;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Enums;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Smoke tests proving that the integration test infrastructure (factory,
/// in-memory DB swap, JWT helper, authenticated client, seed helpers) actually
/// boots and behaves as expected. These tests do NOT exercise application
/// behavior beyond what is needed to verify the harness — those tests live in
/// the dedicated integration test files starting at Branch 16.
/// </summary>
public class InfrastructureSmokeTests : IntegrationTestBase
{
    public InfrastructureSmokeTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public void Factory_CanCreateUnauthenticatedClient()
    {
        var client = CreateClient();
        client.Should().NotBeNull();
        client.BaseAddress.Should().NotBeNull();
    }

    [Fact]
    public async Task Factory_UnauthenticatedRequest_ToProtectedEndpoint_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Factory_AuthenticatedRequest_ToMeEndpoint_ReturnsUser()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(name: "Smoke User", email: "smoke@example.com");
        var client = CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<UserDto>(response);
        body.Should().NotBeNull();
        body!.Email.Should().Be("smoke@example.com");
        body.Name.Should().Be("Smoke User");
    }

    [Fact]
    public async Task Factory_ExpiredToken_IsRejectedWith401()
    {
        await ResetDatabaseAsync();
        var user = await SeedUserAsync(name: "Expired", email: "expired@example.com");
        var client = CreateClientWithExpiredToken(user);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Factory_MalformedToken_IsRejectedWith401()
    {
        var client = CreateClientWithMalformedToken();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Factory_RegisterEndpoint_WorksThroughFullPipeline()
    {
        await ResetDatabaseAsync();
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Pipeline User",
            email = "pipeline@example.com",
            password = "password123"
        });

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await ReadJsonAsync<AuthResponse>(response);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("pipeline@example.com");
    }

    [Fact]
    public async Task Factory_DbContext_IsAccessibleAndUsesInMemoryProvider()
    {
        await using var db = Factory.CreateDbContext();

        db.Database.ProviderName.Should().Contain("InMemory");
    }

    [Fact]
    public async Task ResetDatabaseAsync_RemovesAllRowsAcrossAllTables()
    {
        await ResetDatabaseAsync();
        await using (var arrangeDb = Factory.CreateDbContext())
        {
            await TestSeedHelper.SeedFullScenarioAsync(arrangeDb);
        }

        await ResetDatabaseAsync();

        await using var assertDb = Factory.CreateDbContext();
        assertDb.Users.Should().BeEmpty();
        assertDb.Companies.Should().BeEmpty();
        assertDb.Contacts.Should().BeEmpty();
        assertDb.Tags.Should().BeEmpty();
        assertDb.Deals.Should().BeEmpty();
        assertDb.Activities.Should().BeEmpty();
        assertDb.AiInsights.Should().BeEmpty();
    }

    [Fact]
    public async Task TestSeedHelper_SeedFullScenario_CreatesExpectedShape()
    {
        await ResetDatabaseAsync();
        await using var db = Factory.CreateDbContext();

        var scenario = await TestSeedHelper.SeedFullScenarioAsync(db);

        scenario.User.Id.Should().BeGreaterThan(0);
        scenario.Company.UserId.Should().Be(scenario.User.Id);
        scenario.Contact1.CompanyId.Should().Be(scenario.Company.Id);
        scenario.Contact2.CompanyId.Should().Be(scenario.Company.Id);
        scenario.LeadDeal.Stage.Should().Be(DealStage.Lead);
        scenario.WonDeal.Stage.Should().Be(DealStage.Won);
        scenario.LostDeal.Stage.Should().Be(DealStage.Lost);
        scenario.CallActivity.Type.Should().Be(ActivityType.Call);
        scenario.MeetingActivity.Type.Should().Be(ActivityType.Meeting);
        scenario.EmailActivity.Type.Should().Be(ActivityType.Email);
        scenario.NoteActivity.Type.Should().Be(ActivityType.Note);
    }

    [Fact]
    public async Task TestAiService_ReturnsDeterministicSummary()
    {
        var service = new TestAiService();

        var summary = await service.SummarizeAsync("any input");

        summary.Should().Be(TestAiService.SummaryText);
    }

    [Fact]
    public async Task TestAiService_ReturnsDeterministicDealInsight()
    {
        var service = new TestAiService();
        var deal = new Core.Entities.Deal
        {
            Title = "Test",
            ContactId = 1,
            UserId = 1
        };

        var insight = await service.GenerateDealInsightsAsync(deal, new List<Core.Entities.Activity>());

        insight.Should().Be(TestAiService.InsightText);
    }

    [Fact]
    public async Task TestAiService_SmartSearch_ReturnsInterpretationAndProjectedResults()
    {
        var service = new TestAiService();
        var contact = new Core.Entities.Contact
        {
            Id = 1,
            UserId = 1,
            FirstName = "Alice",
            LastName = "Anderson"
        };
        var activity = new Core.Entities.Activity
        {
            Id = 1,
            UserId = 1,
            Type = ActivityType.Call,
            Subject = "Discovery"
        };

        var result = await service.SmartSearchAsync(
            "anything",
            new List<Core.Entities.Contact> { contact },
            new List<Core.Entities.Activity> { activity });

        result.Interpretation.Should().Be(TestAiService.InterpretationText);
        result.Contacts.Should().HaveCount(1);
        result.Contacts[0].FirstName.Should().Be("Alice");
        result.Activities.Should().HaveCount(1);
        result.Activities[0].Subject.Should().Be("Discovery");
        result.Activities[0].Type.Should().Be("Call");
    }

    [Fact]
    public void JwtTestHelper_GeneratesNonEmptyToken()
    {
        var user = new Core.Entities.User
        {
            Id = 42,
            Name = "Helper Test",
            Email = "helper@example.com",
            Password = "x",
            Role = UserRole.Admin
        };

        var token = JwtTestHelper.GenerateToken(user);

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void JwtTestHelper_GeneratesExpiredToken()
    {
        var user = new Core.Entities.User
        {
            Id = 1,
            Name = "Expired",
            Email = "x@y.z",
            Password = "x",
            Role = UserRole.User
        };

        var token = JwtTestHelper.GenerateExpiredToken(user);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void JwtTestHelper_GeneratesMalformedToken()
    {
        var token = JwtTestHelper.GenerateMalformedToken();

        token.Should().Be("this.is.not.a.valid.jwt");
    }
}
