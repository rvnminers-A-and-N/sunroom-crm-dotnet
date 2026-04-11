using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Services;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Services;

public class OllamaAiServiceTests
{
    private const string DefaultBase = "http://localhost:11434";
    private const string DefaultModel = "llama3";

    private static IConfiguration BuildConfig(
        string? baseUrl = "http://test.local:1234",
        string? model = "test-model")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = baseUrl,
                ["Ollama:Model"] = model
            })
            .Build();
    }

    private static OllamaAiService BuildService(StubHttpMessageHandler handler, IConfiguration? config = null)
    {
        var http = new HttpClient(handler);
        return new OllamaAiService(http, config ?? BuildConfig(), NullLogger<OllamaAiService>.Instance);
    }

    private static string OllamaResponseJson(string responseText)
    {
        return JsonSerializer.Serialize(new
        {
            model = "test-model",
            response = responseText,
            done = true
        });
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsResponseText_FromOllama()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("This is a summary."));
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("Some long notes");

        result.Should().Be("This is a summary.");
    }

    [Fact]
    public async Task SummarizeAsync_PostsPromptContainingInputText()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);

        await service.SummarizeAsync("the quick brown fox");

        handler.RequestBodies.Should().ContainSingle();
        handler.RequestBodies[0].Should().Contain("the quick brown fox");
        handler.RequestBodies[0].Should().Contain("Summarize");
    }

    [Fact]
    public async Task SummarizeAsync_PostsToConfiguredBaseUrl()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler, BuildConfig(baseUrl: "http://custom.host:9999"));

        await service.SummarizeAsync("text");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.ToString().Should().Be("http://custom.host:9999/api/generate");
    }

    [Fact]
    public async Task SummarizeAsync_FallsBackToDefaultBaseUrl_WhenConfigMissing()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler, BuildConfig(baseUrl: null));

        await service.SummarizeAsync("text");

        handler.Requests[0].RequestUri!.ToString().Should().Be($"{DefaultBase}/api/generate");
    }

    [Fact]
    public async Task SummarizeAsync_UsesConfiguredModelInRequestBody()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler, BuildConfig(model: "fancy-llama:70b"));

        await service.SummarizeAsync("text");

        handler.RequestBodies[0].Should().Contain("fancy-llama:70b");
    }

    [Fact]
    public async Task SummarizeAsync_FallsBackToDefaultModel_WhenConfigMissing()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler, BuildConfig(model: null));

        await service.SummarizeAsync("text");

        handler.RequestBodies[0].Should().Contain(DefaultModel);
    }

    [Fact]
    public async Task SummarizeAsync_RequestBodyDisablesStreaming()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);

        await service.SummarizeAsync("text");

        // Should be JSON with stream:false (the service does not handle streamed chunks).
        var doc = JsonDocument.Parse(handler.RequestBodies[0]);
        doc.RootElement.GetProperty("stream").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsFallbackText_WhenHttpFails()
    {
        var handler = StubHttpMessageHandler.ReturnsError();
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("text");

        result.Should().Contain("AI service is currently unavailable");
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsFallbackText_WhenHandlerThrows()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("connection refused"));
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("text");

        result.Should().Contain("AI service is currently unavailable");
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsFallbackText_WhenResponseIsMalformedJson()
    {
        var handler = StubHttpMessageHandler.ReturnsOk("not-valid-json");
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("text");

        result.Should().Contain("AI service is currently unavailable");
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsFallbackText_WhenResponseFieldMissing()
    {
        // Valid JSON but no "response" property — GetProperty will throw, caught by the service.
        var handler = StubHttpMessageHandler.ReturnsOk("{\"model\":\"x\",\"done\":true}");
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("text");

        result.Should().Contain("AI service is currently unavailable");
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsEmptyString_WhenResponseFieldIsExplicitlyNull()
    {
        var handler = StubHttpMessageHandler.ReturnsOk("{\"response\":null}");
        var service = BuildService(handler);

        var result = await service.SummarizeAsync("text");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_ReturnsResponseText()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("Insight text."));
        var service = BuildService(handler);
        var deal = new Deal { Title = "Big Deal", Value = 50000m, Stage = DealStage.Negotiation };

        var result = await service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().Be("Insight text.");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_PromptIncludesTitleValueAndStage()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var deal = new Deal { Title = "Acme Renewal", Value = 12345m, Stage = DealStage.Proposal };

        await service.GenerateDealInsightsAsync(deal, new List<Activity>());

        var body = handler.RequestBodies[0];
        body.Should().Contain("Acme Renewal");
        body.Should().Contain("Proposal");
        body.Should().Contain("12,345"); // Value formatted with N2.
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_PromptIncludesActivityHistory()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var deal = new Deal { Title = "T", Value = 1m, Stage = DealStage.Lead };
        var history = new List<Activity>
        {
            new() { Type = ActivityType.Call, Subject = "Discovery", Body = "Talked through requirements" },
            new() { Type = ActivityType.Email, Subject = "Follow-up", Body = "Sent recap" }
        };

        await service.GenerateDealInsightsAsync(deal, history);

        var body = handler.RequestBodies[0];
        body.Should().Contain("Discovery");
        body.Should().Contain("Follow-up");
        body.Should().Contain("Talked through requirements");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_HandlesNullActivityBody()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var deal = new Deal { Title = "T", Value = 1m, Stage = DealStage.Lead };
        var history = new List<Activity>
        {
            new() { Type = ActivityType.Note, Subject = "No body", Body = null }
        };

        await service.GenerateDealInsightsAsync(deal, history);

        // Service substitutes "No details" for null bodies.
        handler.RequestBodies[0].Should().Contain("No details");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_HandlesEmptyHistory()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("Insights"));
        var service = BuildService(handler);
        var deal = new Deal { Title = "Empty", Value = 1m, Stage = DealStage.Lead };

        var result = await service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().Be("Insights");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_ReturnsFallback_OnHttpError()
    {
        var handler = StubHttpMessageHandler.ReturnsError(HttpStatusCode.BadGateway);
        var service = BuildService(handler);
        var deal = new Deal { Title = "T", Value = 1m, Stage = DealStage.Lead };

        var result = await service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().Contain("AI service is currently unavailable");
    }

    [Fact]
    public async Task SmartSearchAsync_PutsResponseTextInInterpretation()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("Looking for John"));
        var service = BuildService(handler);

        var result = await service.SmartSearchAsync("john", new List<Contact>(), new List<Activity>());

        result.Interpretation.Should().Be("Looking for John");
    }

    [Fact]
    public async Task SmartSearchAsync_AlwaysReturnsEmptyContactsAndActivities()
    {
        // Current implementation places ID resolution back on the caller; the
        // service only returns the model's freeform interpretation.
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("Found stuff"));
        var service = BuildService(handler);
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "Alice", LastName = "X", Email = "a@x.com" }
        };
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Call" }
        };

        var result = await service.SmartSearchAsync("query", contacts, activities);

        result.Contacts.Should().BeEmpty();
        result.Activities.Should().BeEmpty();
    }

    [Fact]
    public async Task SmartSearchAsync_PromptIncludesQueryContactsAndActivities()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var company = new Company { Id = 1, Name = "Acme" };
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com", Company = company, CompanyId = 1 }
        };
        var activities = new List<Activity>
        {
            new()
            {
                Id = 5,
                Type = ActivityType.Meeting,
                Subject = "Kickoff",
                Contact = new Contact { FirstName = "Alice", LastName = "Smith" }
            }
        };

        await service.SmartSearchAsync("kickoff", contacts, activities);

        var body = handler.RequestBodies[0];
        body.Should().Contain("kickoff");
        body.Should().Contain("Alice");
        body.Should().Contain("Acme");
        body.Should().Contain("Kickoff");
    }

    [Fact]
    public async Task SmartSearchAsync_HandlesNullCompanyOnContact()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "Solo", LastName = "Contact", Email = "solo@example.com", Company = null }
        };

        await service.SmartSearchAsync("solo", contacts, new List<Activity>());

        handler.RequestBodies[0].Should().Contain("N/A");
    }

    [Fact]
    public async Task SmartSearchAsync_TruncatesToFirstFiftyContactsAndActivities()
    {
        var handler = StubHttpMessageHandler.ReturnsOk(OllamaResponseJson("ok"));
        var service = BuildService(handler);
        var contacts = Enumerable.Range(1, 75).Select(i => new Contact
        {
            Id = i,
            FirstName = $"First{i}",
            LastName = "Last",
            Email = $"f{i}@example.com"
        }).ToList();
        var activities = Enumerable.Range(1, 75).Select(i => new Activity
        {
            Id = i,
            Type = ActivityType.Note,
            Subject = $"Subj{i}"
        }).ToList();

        await service.SmartSearchAsync("query", contacts, activities);

        var body = handler.RequestBodies[0];
        body.Should().Contain("First1");
        body.Should().Contain("First50");
        body.Should().NotContain("First51"); // Truncated.
        body.Should().Contain("Subj1");
        body.Should().Contain("Subj50");
        body.Should().NotContain("Subj51");
    }

    [Fact]
    public async Task SmartSearchAsync_ReturnsFallback_OnHttpError()
    {
        var handler = StubHttpMessageHandler.ReturnsError();
        var service = BuildService(handler);

        var result = await service.SmartSearchAsync("anything", new List<Contact>(), new List<Activity>());

        result.Interpretation.Should().Contain("AI service is currently unavailable");
        result.Contacts.Should().BeEmpty();
        result.Activities.Should().BeEmpty();
    }
}
