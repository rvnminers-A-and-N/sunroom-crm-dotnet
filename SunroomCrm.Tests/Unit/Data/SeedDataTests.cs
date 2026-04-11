using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Data;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Data;

public class SeedDataTests
{
    [Fact]
    public async Task SeedAsync_PopulatesAllResources_WhenDatabaseIsEmpty()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        db.Users.Should().HaveCount(3);
        db.Tags.Should().HaveCount(6);
        db.Companies.Should().HaveCount(5);
        db.Contacts.Should().HaveCount(8);
        db.Deals.Should().HaveCount(7);
        db.Activities.Should().HaveCount(10);
        db.AiInsights.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedAsync_DoesNothing_WhenUsersAlreadyExist()
    {
        using var db = TestDbContext.Create();
        await TestSeedHelper.SeedUserAsync(db);
        var initialUserCount = db.Users.Count();

        await SeedData.SeedAsync(db);

        db.Users.Should().HaveCount(initialUserCount);
        db.Tags.Should().BeEmpty();
        db.Companies.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenCalledTwice()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);
        var firstUserCount = db.Users.Count();
        await SeedData.SeedAsync(db);

        db.Users.Should().HaveCount(firstUserCount);
    }

    [Fact]
    public async Task SeedAsync_CreatesAdminUser()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var admin = db.Users.SingleOrDefault(u => u.Email == "admin@sunroomcrm.com");
        admin.Should().NotBeNull();
        admin!.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SeedAsync_CreatesManagerUser()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var manager = db.Users.SingleOrDefault(u => u.Email == "sarah@sunroomcrm.com");
        manager.Should().NotBeNull();
        manager!.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public async Task SeedAsync_CreatesStandardUser()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var user = db.Users.SingleOrDefault(u => u.Email == "jake@sunroomcrm.com");
        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task SeedAsync_HashesUserPasswords()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var admin = db.Users.Single(u => u.Email == "admin@sunroomcrm.com");
        admin.Password.Should().NotBe("password123");
        BCrypt.Net.BCrypt.Verify("password123", admin.Password).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_CreatesSixUniquelyNamedTags()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var tagNames = db.Tags.Select(t => t.Name).ToList();
        tagNames.Should().BeEquivalentTo(new[]
        {
            "VIP", "Hot Lead", "Decision Maker", "Referral", "Follow Up", "Cold"
        });
    }

    [Fact]
    public async Task SeedAsync_AssignsTagsToContacts()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        // John Smith should have VIP and Decision Maker.
        var john = db.Contacts
            .Where(c => c.FirstName == "John" && c.LastName == "Smith")
            .Select(c => new { c.Id, Tags = c.Tags.Select(t => t.Name).ToList() })
            .Single();
        john.Tags.Should().Contain(new[] { "VIP", "Decision Maker" });
    }

    [Fact]
    public async Task SeedAsync_CreatesDealsAcrossAllStages()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var stages = db.Deals.Select(d => d.Stage).Distinct().ToList();
        stages.Should().Contain(new[]
        {
            DealStage.Lead,
            DealStage.Qualified,
            DealStage.Proposal,
            DealStage.Negotiation,
            DealStage.Won,
            DealStage.Lost
        });
    }

    [Fact]
    public async Task SeedAsync_LinksDealsToContactsAndCompanies()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        db.Deals.Should().AllSatisfy(d => d.ContactId.Should().BeGreaterThan(0));
        db.Deals.Where(d => d.Title.Contains("Acme")).Should().AllSatisfy(d => d.CompanyId.Should().NotBeNull());
    }

    [Fact]
    public async Task SeedAsync_CreatesAiInsightsForDeals()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var dealIds = db.Deals.Select(d => d.Id).ToList();
        db.AiInsights.Should().AllSatisfy(ai => dealIds.Should().Contain(ai.DealId));
    }

    [Fact]
    public async Task SeedAsync_CreatesActivitiesAcrossTypes()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var types = db.Activities.Select(a => a.Type).Distinct().ToList();
        types.Should().Contain(new[]
        {
            ActivityType.Call,
            ActivityType.Email,
            ActivityType.Meeting,
            ActivityType.Note,
            ActivityType.Task
        });
    }

    [Fact]
    public async Task SeedAsync_CreatesContactWithoutCompany()
    {
        using var db = TestDbContext.Create();

        await SeedData.SeedAsync(db);

        var freelancer = db.Contacts.SingleOrDefault(c => c.Email == "lisa@freelance.example.com");
        freelancer.Should().NotBeNull();
        freelancer!.CompanyId.Should().BeNull();
    }
}
