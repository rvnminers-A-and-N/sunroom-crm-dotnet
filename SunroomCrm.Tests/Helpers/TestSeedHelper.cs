using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Tests.Helpers;

/// <summary>
/// Builds populated database states for tests that need a non-trivial baseline
/// (dashboard aggregation, pagination, search, isolation tests). All seed
/// methods take an <see cref="AppDbContext"/> so they work with both unit
/// tests (via <see cref="TestDbContext.Create"/>) and integration tests
/// (via the WebApplicationFactory's scoped context).
/// </summary>
public static class TestSeedHelper
{
    /// <summary>
    /// Inserts a single user. Returns the persisted entity.
    /// </summary>
    public static async Task<User> SeedUserAsync(
        AppDbContext db,
        string name = "Test User",
        string? email = null,
        UserRole role = UserRole.User,
        string password = "password123")
    {
        var user = new User
        {
            Name = name,
            Email = email ?? $"user_{Guid.NewGuid():N}@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Inserts a tag with the given name and color.
    /// </summary>
    public static async Task<Tag> SeedTagAsync(
        AppDbContext db,
        string name = "Test Tag",
        string color = "#02795F")
    {
        var tag = new Tag { Name = name, Color = color };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    /// <summary>
    /// Inserts a company owned by the given user.
    /// </summary>
    public static async Task<Company> SeedCompanyAsync(
        AppDbContext db,
        int userId,
        string name = "Acme Corp",
        string? industry = "Technology",
        string? city = "Austin",
        string? state = "TX")
    {
        var company = new Company
        {
            UserId = userId,
            Name = name,
            Industry = industry,
            City = city,
            State = state
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    /// <summary>
    /// Inserts a contact owned by the given user, optionally linked to a company.
    /// </summary>
    public static async Task<Contact> SeedContactAsync(
        AppDbContext db,
        int userId,
        int? companyId = null,
        string firstName = "John",
        string lastName = "Doe",
        string? email = null)
    {
        var contact = new Contact
        {
            UserId = userId,
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Email = email ?? $"{firstName.ToLower()}.{lastName.ToLower()}@example.com",
            Title = "Engineer"
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    /// <summary>
    /// Inserts a deal owned by the given user.
    /// </summary>
    public static async Task<Deal> SeedDealAsync(
        AppDbContext db,
        int userId,
        int contactId,
        int? companyId = null,
        string title = "Test Deal",
        decimal value = 10000m,
        DealStage stage = DealStage.Lead)
    {
        var deal = new Deal
        {
            UserId = userId,
            ContactId = contactId,
            CompanyId = companyId,
            Title = title,
            Value = value,
            Stage = stage
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }

    /// <summary>
    /// Inserts an activity owned by the given user.
    /// </summary>
    public static async Task<Activity> SeedActivityAsync(
        AppDbContext db,
        int userId,
        int? contactId = null,
        int? dealId = null,
        ActivityType type = ActivityType.Note,
        string subject = "Test Activity",
        string? body = "Test activity body")
    {
        var activity = new Activity
        {
            UserId = userId,
            ContactId = contactId,
            DealId = dealId,
            Type = type,
            Subject = subject,
            Body = body,
            OccurredAt = DateTime.UtcNow
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return activity;
    }

    /// <summary>
    /// Builds a fully-populated scenario: one user, one company, two contacts,
    /// three deals across stages, and four activities. Returns a record with
    /// references to every seeded entity for assertions.
    /// </summary>
    public static async Task<SeedScenario> SeedFullScenarioAsync(AppDbContext db)
    {
        var user = await SeedUserAsync(db, name: "Scenario User", email: "scenario@example.com");

        var company = await SeedCompanyAsync(db, user.Id, name: "Scenario Co");

        var contact1 = await SeedContactAsync(db, user.Id, company.Id, "Alice", "Anderson");
        var contact2 = await SeedContactAsync(db, user.Id, company.Id, "Bob", "Brown");

        var deal1 = await SeedDealAsync(db, user.Id, contact1.Id, company.Id,
            title: "Lead Deal", value: 5000m, stage: DealStage.Lead);
        var deal2 = await SeedDealAsync(db, user.Id, contact1.Id, company.Id,
            title: "Won Deal", value: 25000m, stage: DealStage.Won);
        var deal3 = await SeedDealAsync(db, user.Id, contact2.Id, company.Id,
            title: "Lost Deal", value: 8000m, stage: DealStage.Lost);

        var activity1 = await SeedActivityAsync(db, user.Id, contact1.Id, deal1.Id,
            ActivityType.Call, "Intro call");
        var activity2 = await SeedActivityAsync(db, user.Id, contact1.Id, deal2.Id,
            ActivityType.Meeting, "Closing meeting");
        var activity3 = await SeedActivityAsync(db, user.Id, contact2.Id, deal3.Id,
            ActivityType.Email, "Follow up email");
        var activity4 = await SeedActivityAsync(db, user.Id, contact2.Id, null,
            ActivityType.Note, "Contact note");

        return new SeedScenario(
            user, company,
            contact1, contact2,
            deal1, deal2, deal3,
            activity1, activity2, activity3, activity4);
    }
}

/// <summary>
/// Strongly-typed result of <see cref="TestSeedHelper.SeedFullScenarioAsync"/>.
/// </summary>
public record SeedScenario(
    User User,
    Company Company,
    Contact Contact1,
    Contact Contact2,
    Deal LeadDeal,
    Deal WonDeal,
    Deal LostDeal,
    Activity CallActivity,
    Activity MeetingActivity,
    Activity EmailActivity,
    Activity NoteActivity);
