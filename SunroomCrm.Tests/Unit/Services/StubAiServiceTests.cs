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

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsSameSummaryRegardlessOfInput()
    {
        // Stub returns a fixed canned response — verify it's deterministic.
        var first = await _service.SummarizeAsync("anything");
        var second = await _service.SummarizeAsync("something completely different");

        first.Should().Be(second);
    }

    [Fact]
    public async Task SummarizeAsync_AcceptsEmptyString()
    {
        var result = await _service.SummarizeAsync(string.Empty);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_IncludesDealTitle()
    {
        var deal = new Deal
        {
            Title = "Enterprise License",
            Value = 100000m,
            Stage = DealStage.Negotiation
        };

        var result = await _service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().Contain("Enterprise License");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_IncludesDealStage()
    {
        var deal = new Deal
        {
            Title = "Sample",
            Stage = DealStage.Proposal
        };

        var result = await _service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().Contain("Proposal");
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_DoesNotThrow_WithEmptyHistory()
    {
        var deal = new Deal { Title = "Empty History", Stage = DealStage.Lead };

        var result = await _service.GenerateDealInsightsAsync(deal, new List<Activity>());

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateDealInsightsAsync_DoesNotThrow_WithRichHistory()
    {
        var deal = new Deal { Title = "Rich History", Stage = DealStage.Lead };
        var history = Enumerable.Range(0, 25).Select(i => new Activity
        {
            Id = i,
            Type = ActivityType.Note,
            Subject = $"Note {i}",
            Body = $"Body {i}"
        }).ToList();

        var result = await _service.GenerateDealInsightsAsync(deal, history);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SmartSearchAsync_FindsContactByFirstName()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "John", LastName = "Smith", Email = "john@example.com", Tags = new List<Tag>() },
            new() { Id = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", Tags = new List<Tag>() }
        };

        var result = await _service.SmartSearchAsync("John", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle(c => c.FirstName == "John");
    }

    [Fact]
    public async Task SmartSearchAsync_FindsContactByLastName()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "John", LastName = "Smith", Email = "j@example.com", Tags = new List<Tag>() },
            new() { Id = 2, FirstName = "Jane", LastName = "Doe", Email = "jd@example.com", Tags = new List<Tag>() }
        };

        var result = await _service.SmartSearchAsync("doe", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle(c => c.LastName == "Doe");
    }

    [Fact]
    public async Task SmartSearchAsync_FindsContactByEmail()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "X", LastName = "Y", Email = "alice@example.com", Tags = new List<Tag>() },
            new() { Id = 2, FirstName = "P", LastName = "Q", Email = "bob@example.com", Tags = new List<Tag>() }
        };

        var result = await _service.SmartSearchAsync("alice", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle();
        result.Contacts.First().Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task SmartSearchAsync_FindsContactByCompanyName()
    {
        var company = new Company { Id = 1, Name = "Acme Corp" };
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "X", LastName = "Y", Email = "x@y.com", Tags = new List<Tag>(), Company = company, CompanyId = 1 },
            new() { Id = 2, FirstName = "P", LastName = "Q", Email = "p@q.com", Tags = new List<Tag>() }
        };

        var result = await _service.SmartSearchAsync("acme", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle();
        result.Contacts.First().CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task SmartSearchAsync_HandlesNullEmail()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "John", LastName = "Smith", Email = null, Tags = new List<Tag>() }
        };

        // Should not throw despite null email.
        var result = await _service.SmartSearchAsync("john", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle();
    }

    [Fact]
    public async Task SmartSearchAsync_HandlesNullCompany()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "John", LastName = "Smith", Email = "john@example.com", Tags = new List<Tag>(), Company = null }
        };

        var result = await _service.SmartSearchAsync("john", contacts, new List<Activity>());

        result.Contacts.Should().ContainSingle();
    }

    [Fact]
    public async Task SmartSearchAsync_LimitsContactsToTen()
    {
        var contacts = Enumerable.Range(0, 25)
            .Select(i => new Contact
            {
                Id = i,
                FirstName = $"Match{i}",
                LastName = "X",
                Email = $"match{i}@example.com",
                Tags = new List<Tag>()
            })
            .ToList();

        var result = await _service.SmartSearchAsync("match", contacts, new List<Activity>());

        result.Contacts.Should().HaveCount(10);
    }

    [Fact]
    public async Task SmartSearchAsync_FindsActivityBySubject()
    {
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Budget Discussion", Body = "Pricing", User = new User { Name = "Rep" } },
            new() { Id = 2, Type = ActivityType.Note, Subject = "Internal", Body = "Sync", User = new User { Name = "Rep" } }
        };

        var result = await _service.SmartSearchAsync("budget", new List<Contact>(), activities);

        result.Activities.Should().ContainSingle(a => a.Subject == "Budget Discussion");
    }

    [Fact]
    public async Task SmartSearchAsync_FindsActivityByBody()
    {
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Random", Body = "We discussed pricing", User = new User { Name = "Rep" } },
            new() { Id = 2, Type = ActivityType.Note, Subject = "Other", Body = "Nothing useful", User = new User { Name = "Rep" } }
        };

        var result = await _service.SmartSearchAsync("pricing", new List<Contact>(), activities);

        result.Activities.Should().ContainSingle();
    }

    [Fact]
    public async Task SmartSearchAsync_HandlesNullActivityBody()
    {
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Match Subject", Body = null, User = new User { Name = "Rep" } }
        };

        var result = await _service.SmartSearchAsync("match", new List<Contact>(), activities);

        result.Activities.Should().ContainSingle();
    }

    [Fact]
    public async Task SmartSearchAsync_LimitsActivitiesToTen()
    {
        var activities = Enumerable.Range(0, 25)
            .Select(i => new Activity
            {
                Id = i,
                Type = ActivityType.Note,
                Subject = $"Match{i}",
                Body = $"Body{i}",
                User = new User { Name = "Rep" }
            })
            .ToList();

        var result = await _service.SmartSearchAsync("match", new List<Contact>(), activities);

        result.Activities.Should().HaveCount(10);
    }

    [Fact]
    public async Task SmartSearchAsync_IncludesQueryInInterpretation()
    {
        var result = await _service.SmartSearchAsync("widgets", new List<Contact>(), new List<Activity>());

        result.Interpretation.Should().Contain("widgets");
    }

    [Fact]
    public async Task SmartSearchAsync_ReturnsEmptyResults_WhenNothingMatches()
    {
        var contacts = new List<Contact>
        {
            new() { Id = 1, FirstName = "Alice", LastName = "X", Email = "a@x.com", Tags = new List<Tag>() }
        };
        var activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Note, Subject = "Internal", Body = "Sync", User = new User { Name = "R" } }
        };

        var result = await _service.SmartSearchAsync("zzz-nothing", contacts, activities);

        result.Contacts.Should().BeEmpty();
        result.Activities.Should().BeEmpty();
    }

    [Fact]
    public async Task SmartSearchAsync_MapsContactTagsToDtos()
    {
        var tag = new Tag { Id = 7, Name = "VIP", Color = "#FFD700" };
        var contacts = new List<Contact>
        {
            new()
            {
                Id = 1, FirstName = "Tagged", LastName = "User",
                Email = "tagged@example.com", Tags = new List<Tag> { tag }
            }
        };

        var result = await _service.SmartSearchAsync("tagged", contacts, new List<Activity>());

        var dto = result.Contacts.Should().ContainSingle().Subject;
        dto.Tags.Should().ContainSingle();
        dto.Tags.First().Name.Should().Be("VIP");
        dto.Tags.First().Color.Should().Be("#FFD700");
    }

    [Fact]
    public async Task SmartSearchAsync_MapsActivityContactAndDealMetadata()
    {
        var contact = new Contact { Id = 1, FirstName = "Carl", LastName = "Lewis" };
        var deal = new Deal { Id = 5, Title = "Olympics" };
        var activities = new List<Activity>
        {
            new()
            {
                Id = 1,
                Type = ActivityType.Call,
                Subject = "Race plan",
                Body = "Discussed strategy",
                Contact = contact,
                ContactId = contact.Id,
                Deal = deal,
                DealId = deal.Id,
                User = new User { Name = "Coach" }
            }
        };

        var result = await _service.SmartSearchAsync("race", new List<Contact>(), activities);

        var dto = result.Activities.Should().ContainSingle().Subject;
        dto.ContactName.Should().Be("Carl Lewis");
        dto.DealTitle.Should().Be("Olympics");
        dto.UserName.Should().Be("Coach");
    }

    [Fact]
    public async Task SmartSearchAsync_HandlesNullActivityUser()
    {
        var activities = new List<Activity>
        {
            new()
            {
                Id = 1,
                Type = ActivityType.Note,
                Subject = "Orphaned",
                Body = "No user attached",
                User = null!
            }
        };

        var result = await _service.SmartSearchAsync("orphaned", new List<Contact>(), activities);

        result.Activities.Should().ContainSingle();
        result.Activities.First().UserName.Should().Be(string.Empty);
    }
}
