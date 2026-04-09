using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Services;

namespace SunroomCrm.Tests.Unit.Services;

public class StubAiServiceTests
{
    private readonly StubAiService _service = new();

    [Fact]
    public async Task SummarizeAsync_ReturnsNonEmptySummary()
    {
        var result = await _service.SummarizeAsync("Some long meeting notes about the project.");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_ReturnsInsightsWithDealTitle()
    {
        var deal = new Deal
        {
            Title = "Enterprise License",
            Value = 100000m,
            Stage = DealStage.Negotiation
        };
        var history = new List<Activity>
        {
            new() { Type = ActivityType.Call, Subject = "Discovery Call", Body = "Discussed requirements" }
        };

        var result = await _service.GenerateDealInsightsAsync(deal, history);

        Assert.NotNull(result);
        Assert.Contains("Enterprise License", result);
    }

    [Fact]
    public async Task SmartSearchAsync_ReturnsMatchingContacts()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "John", LastName = "Smith", Email = "john@example.com", Tags = new List<Tag>() },
            new() { Id = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", Tags = new List<Tag>() }
        };
        var activities = new List<Activity>();

        var result = await _service.SmartSearchAsync("John", contacts, activities);

        Assert.NotNull(result);
        Assert.Contains(result.Contacts, c => c.FirstName == "John");
        Assert.DoesNotContain(result.Contacts, c => c.FirstName == "Jane");
    }

    [Fact]
    public async Task SmartSearchAsync_ReturnsMatchingActivities()
    {
        var contacts = new List<Contact>();
        var user = new User { Id = 1, Name = "Test" };
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Budget Discussion", Body = "Talked about pricing", User = user },
            new() { Id = 2, Type = ActivityType.Note, Subject = "Internal Notes", Body = "Team sync", User = user }
        };

        var result = await _service.SmartSearchAsync("budget", contacts, activities);

        Assert.Single(result.Activities);
        Assert.Equal("Budget Discussion", result.Activities[0].Subject);
    }
}
