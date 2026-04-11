using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class ActivityRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsActivityToDatabase()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);

        var result = await repo.CreateAsync(TestDataFactory.CreateActivity(
            user.Id, type: ActivityType.Note, subject: "Quick note"));

        result.Id.Should().BeGreaterThan(0);
        result.Subject.Should().Be("Quick note");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_LoadsUserContactAndDeal()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var company = await TestSeedHelper.SeedCompanyAsync(db, user.Id);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id, company.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, company.Id);
        var repo = new ActivityRepository(db);
        var activity = await repo.CreateAsync(TestDataFactory.CreateActivity(
            user.Id, contact.Id, deal.Id, ActivityType.Call, "Intro"));

        var result = await repo.GetByIdAsync(activity.Id);

        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.User.Id.Should().Be(user.Id);
        result.Contact.Should().NotBeNull();
        result.Contact!.Id.Should().Be(contact.Id);
        result.Deal.Should().NotBeNull();
        result.Deal!.Id.Should().Be(deal.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new ActivityRepository(db);

        (await repo.GetByIdAsync(9999)).Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_FiltersByUser()
    {
        using var db = TestDbContext.Create();
        var owner = await TestSeedHelper.SeedUserAsync(db, email: "o@example.com");
        var stranger = await TestSeedHelper.SeedUserAsync(db, email: "s@example.com");
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(owner.Id, subject: "Mine"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(stranger.Id, subject: "NotMine"));

        var (items, total) = await repo.GetAllAsync(owner.Id, new ActivityFilterParams());

        total.Should().Be(1);
        items.Should().ContainSingle(a => a.Subject == "Mine");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByContactId()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contactA = await TestSeedHelper.SeedContactAsync(db, user.Id, firstName: "Alice", lastName: "A");
        var contactB = await TestSeedHelper.SeedContactAsync(db, user.Id, firstName: "Bob", lastName: "B");
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contactA.Id, subject: "ForAlice"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contactB.Id, subject: "ForBob"));

        var (items, total) = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { ContactId = contactA.Id });

        total.Should().Be(1);
        items.Should().ContainSingle(a => a.Subject == "ForAlice");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDealId()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var dealA = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "DealA");
        var dealB = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "DealB");
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, dealA.Id, subject: "ForA"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, dealB.Id, subject: "ForB"));

        var (items, total) = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { DealId = dealA.Id });

        total.Should().Be(1);
        items.Should().ContainSingle(a => a.Subject == "ForA");
    }

    [Theory]
    [InlineData("call", ActivityType.Call)]
    [InlineData("CALL", ActivityType.Call)]
    [InlineData("Email", ActivityType.Email)]
    [InlineData("meeting", ActivityType.Meeting)]
    [InlineData("note", ActivityType.Note)]
    [InlineData("task", ActivityType.Task)]
    public async Task GetAllAsync_FiltersByType_CaseInsensitive(string typeFilter, ActivityType expected)
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Call, subject: "Call"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Email, subject: "Email"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Meeting, subject: "Meeting"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Note, subject: "Note"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Task, subject: "Task"));

        var (items, total) = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Type = typeFilter });

        total.Should().Be(1);
        items.Should().ContainSingle();
        items.First().Type.Should().Be(expected);
    }

    [Fact]
    public async Task GetAllAsync_InvalidTypeFilterIsIgnored()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Note));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Call));

        var (items, total) = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Type = "not-a-real-type" });

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WhitespaceTypeFilterIsIgnored()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Note));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, type: ActivityType.Call));

        var (items, total) = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Type = "   " });

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByOccurredAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        var older = TestDataFactory.CreateActivity(user.Id, subject: "Older");
        older.OccurredAt = DateTime.UtcNow.AddDays(-2);
        await repo.CreateAsync(older);

        var newer = TestDataFactory.CreateActivity(user.Id, subject: "Newer");
        newer.OccurredAt = DateTime.UtcNow;
        await repo.CreateAsync(newer);

        var middle = TestDataFactory.CreateActivity(user.Id, subject: "Middle");
        middle.OccurredAt = DateTime.UtcNow.AddDays(-1);
        await repo.CreateAsync(middle);

        var (items, _) = await repo.GetAllAsync(user.Id, new ActivityFilterParams());

        items.Select(a => a.Subject).Should().ContainInOrder("Newer", "Middle", "Older");
    }

    [Fact]
    public async Task GetAllAsync_PaginatesResults()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        for (int i = 0; i < 5; i++)
        {
            var a = TestDataFactory.CreateActivity(user.Id, subject: $"Act{i}");
            a.OccurredAt = DateTime.UtcNow.AddMinutes(i);
            await repo.CreateAsync(a);
        }

        var pageOne = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Page = 1, PerPage = 2 });
        var pageTwo = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Page = 2, PerPage = 2 });
        var pageThree = await repo.GetAllAsync(
            user.Id, new ActivityFilterParams { Page = 3, PerPage = 2 });

        pageOne.Total.Should().Be(5);
        pageOne.Items.Should().HaveCount(2);
        pageTwo.Items.Should().HaveCount(2);
        pageThree.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_CombinesAllFilters()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new ActivityRepository(db);

        // Match: right contact, right deal, right type.
        await repo.CreateAsync(TestDataFactory.CreateActivity(
            user.Id, contact.Id, deal.Id, ActivityType.Call, "Match"));
        // Wrong type.
        await repo.CreateAsync(TestDataFactory.CreateActivity(
            user.Id, contact.Id, deal.Id, ActivityType.Email, "WrongType"));
        // Wrong deal.
        await repo.CreateAsync(TestDataFactory.CreateActivity(
            user.Id, contact.Id, null, ActivityType.Call, "NoDeal"));

        var (items, total) = await repo.GetAllAsync(user.Id, new ActivityFilterParams
        {
            ContactId = contact.Id,
            DealId = deal.Id,
            Type = "call"
        });

        total.Should().Be(1);
        items.Should().ContainSingle(a => a.Subject == "Match");
    }

    [Fact]
    public async Task UpdateAsync_ModifiesActivity()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        var activity = await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, subject: "Old"));

        activity.Subject = "Updated";
        activity.Body = "New body";
        await repo.UpdateAsync(activity);

        var result = await repo.GetByIdAsync(activity.Id);
        result!.Subject.Should().Be("Updated");
        result.Body.Should().Be("New body");
    }

    [Fact]
    public async Task DeleteAsync_RemovesActivity()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        var activity = await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id));

        await repo.DeleteAsync(activity.Id);

        (await repo.GetByIdAsync(activity.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenActivityDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, subject: "Stays"));

        await repo.DeleteAsync(9999);

        var (items, _) = await repo.GetAllAsync(user.Id, new ActivityFilterParams());
        items.Should().ContainSingle();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenActivityExists()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        var activity = await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id));

        (await repo.ExistsAsync(activity.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenActivityDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new ActivityRepository(db);

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsMostRecentForUser()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        for (int i = 0; i < 15; i++)
        {
            var a = TestDataFactory.CreateActivity(user.Id, subject: $"Act{i}");
            a.OccurredAt = DateTime.UtcNow.AddMinutes(i);
            await repo.CreateAsync(a);
        }

        var recent = await repo.GetRecentAsync(user.Id);

        recent.Should().HaveCount(10);
        recent.First().Subject.Should().Be("Act14");
        recent.Last().Subject.Should().Be("Act5");
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCustomCount()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new ActivityRepository(db);
        for (int i = 0; i < 5; i++)
        {
            var a = TestDataFactory.CreateActivity(user.Id, subject: $"Act{i}");
            a.OccurredAt = DateTime.UtcNow.AddMinutes(i);
            await repo.CreateAsync(a);
        }

        var recent = await repo.GetRecentAsync(user.Id, count: 3);

        recent.Should().HaveCount(3);
        recent.Select(a => a.Subject).Should().ContainInOrder("Act4", "Act3", "Act2");
    }

    [Fact]
    public async Task GetRecentAsync_OnlyReturnsActivitiesForUser()
    {
        using var db = TestDbContext.Create();
        var owner = await TestSeedHelper.SeedUserAsync(db, email: "o@example.com");
        var stranger = await TestSeedHelper.SeedUserAsync(db, email: "s@example.com");
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(owner.Id, subject: "Mine"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(stranger.Id, subject: "NotMine"));

        var recent = await repo.GetRecentAsync(owner.Id);

        recent.Should().ContainSingle(a => a.Subject == "Mine");
    }

    [Fact]
    public async Task GetRecentAsync_IncludesNavigationProperties()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, deal.Id));

        var recent = await repo.GetRecentAsync(user.Id);

        var first = recent.Should().ContainSingle().Subject;
        first.User.Should().NotBeNull();
        first.Contact.Should().NotBeNull();
        first.Deal.Should().NotBeNull();
    }

    [Fact]
    public async Task GetForDealAsync_ReturnsActivitiesForDeal()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var dealA = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "A");
        var dealB = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "B");
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, dealA.Id, subject: "A1"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, dealA.Id, subject: "A2"));
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, dealB.Id, subject: "B1"));

        var result = await repo.GetForDealAsync(dealA.Id);

        result.Should().HaveCount(2);
        result.Select(a => a.Subject).Should().BeEquivalentTo(new[] { "A1", "A2" });
    }

    [Fact]
    public async Task GetForDealAsync_OrdersByOccurredAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new ActivityRepository(db);

        var older = TestDataFactory.CreateActivity(user.Id, contact.Id, deal.Id, subject: "Older");
        older.OccurredAt = DateTime.UtcNow.AddDays(-2);
        await repo.CreateAsync(older);

        var newer = TestDataFactory.CreateActivity(user.Id, contact.Id, deal.Id, subject: "Newer");
        newer.OccurredAt = DateTime.UtcNow;
        await repo.CreateAsync(newer);

        var result = await repo.GetForDealAsync(deal.Id);

        result.Select(a => a.Subject).Should().ContainInOrder("Newer", "Older");
    }

    [Fact]
    public async Task GetForDealAsync_ReturnsEmpty_WhenNoActivitiesForDeal()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new ActivityRepository(db);

        var result = await repo.GetForDealAsync(deal.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForDealAsync_IncludesUserNavigation()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new ActivityRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contact.Id, deal.Id));

        var result = await repo.GetForDealAsync(deal.Id);

        result.Should().ContainSingle().Which.User.Should().NotBeNull();
    }
}
