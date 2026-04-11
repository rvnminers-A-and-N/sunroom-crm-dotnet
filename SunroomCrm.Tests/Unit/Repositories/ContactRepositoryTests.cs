using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class ContactRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsContactToDatabase()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var repo = new ContactRepository(db);
        var contact = TestDataFactory.CreateContact(user.Id);

        var result = await repo.CreateAsync(contact);

        Assert.True(result.Id > 0);
        Assert.Equal("John", result.FirstName);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearch()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Alice", lastName: "Smith"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Bob", lastName: "Jones"));

        var filter = new ContactFilterParams { Search = "alice" };
        var (items, total) = await repo.GetAllAsync(user.Id, filter);

        Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal("Alice", items[0].FirstName);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCompany()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var companyRepo = new CompanyRepository(db);
        var company = await companyRepo.CreateAsync(TestDataFactory.CreateCompany(user.Id));

        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, companyId: company.Id, firstName: "Alice"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Bob"));

        var filter = new ContactFilterParams { CompanyId = company.Id };
        var (items, total) = await repo.GetAllAsync(user.Id, filter);

        Assert.Single(items);
        Assert.Equal("Alice", items[0].FirstName);
    }

    [Fact]
    public async Task SyncTagsAsync_SetsContactTags()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var tagRepo = new TagRepository(db);
        var tag1 = await tagRepo.CreateAsync(TestDataFactory.CreateTag("VIP", "#F76C6C"));
        var tag2 = await tagRepo.CreateAsync(TestDataFactory.CreateTag("Hot Lead", "#F9A66C"));

        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        await repo.SyncTagsAsync(contact.Id, new List<int> { tag1.Id, tag2.Id });

        var result = await repo.GetByIdAsync(contact.Id);
        Assert.Equal(2, result!.Tags.Count);
    }

    [Fact]
    public async Task SyncTagsAsync_ReplacesExistingTags()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var tagRepo = new TagRepository(db);
        var tag1 = await tagRepo.CreateAsync(TestDataFactory.CreateTag("VIP", "#F76C6C"));
        var tag2 = await tagRepo.CreateAsync(TestDataFactory.CreateTag("Hot Lead", "#F9A66C"));
        var tag3 = await tagRepo.CreateAsync(TestDataFactory.CreateTag("Cold", "#6B7280"));

        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        await repo.SyncTagsAsync(contact.Id, new List<int> { tag1.Id, tag2.Id });
        await repo.SyncTagsAsync(contact.Id, new List<int> { tag3.Id });

        var result = await repo.GetByIdAsync(contact.Id);
        Assert.Single(result!.Tags);
        Assert.Equal("Cold", result.Tags.First().Name);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "A"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "B"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "C"));

        Assert.Equal(3, await repo.GetCountAsync(user.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesContact()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());

        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        await repo.DeleteAsync(contact.Id);

        Assert.False(await repo.ExistsAsync(contact.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenContactDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new ContactRepository(db);

        var result = await repo.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesTags()
    {
        using var db = TestDbContext.Create();
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());
        var tagRepo = new TagRepository(db);
        var tag = await tagRepo.CreateAsync(TestDataFactory.CreateTag("VIP"));
        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));
        await repo.SyncTagsAsync(contact.Id, new List<int> { tag.Id });

        var result = await repo.GetByIdAsync(contact.Id);

        result.Should().NotBeNull();
        result!.Tags.Should().ContainSingle().Which.Name.Should().Be("VIP");
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_IncludesCompanyTagsDealsAndActivities()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var company = await new CompanyRepository(db).CreateAsync(TestDataFactory.CreateCompany(user.Id));
        var tag = await new TagRepository(db).CreateAsync(TestDataFactory.CreateTag("Lead"));

        var contactRepo = new ContactRepository(db);
        var contact = await contactRepo.CreateAsync(
            TestDataFactory.CreateContact(user.Id, companyId: company.Id));
        await contactRepo.SyncTagsAsync(contact.Id, new List<int> { tag.Id });

        var dealRepo = new DealRepository(db);
        await dealRepo.CreateAsync(TestDataFactory.CreateDeal(user.Id, contact.Id, company.Id, "Deal A"));

        var activityRepo = new ActivityRepository(db);
        await activityRepo.CreateAsync(TestDataFactory.CreateActivity(user.Id, contactId: contact.Id));

        var result = await contactRepo.GetByIdWithDetailsAsync(contact.Id);

        result.Should().NotBeNull();
        result!.Company.Should().NotBeNull();
        result.Company!.Id.Should().Be(company.Id);
        result.Tags.Should().ContainSingle();
        result.Deals.Should().ContainSingle();
        result.Deals.First().Title.Should().Be("Deal A");
        result.Activities.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenNotFound()
    {
        using var db = TestDbContext.Create();
        var repo = new ContactRepository(db);

        var result = await repo.GetByIdWithDetailsAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearch_OnEmail()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Alice", lastName: "Anderson"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Bob", lastName: "Brown"));

        var (items, _) = await repo.GetAllAsync(user.Id, new ContactFilterParams { Search = "alice.anderson" });

        items.Should().ContainSingle();
        items[0].FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByTagId()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var tag = await new TagRepository(db).CreateAsync(TestDataFactory.CreateTag("VIP"));
        var repo = new ContactRepository(db);
        var tagged = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Tagged"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Untagged"));
        await repo.SyncTagsAsync(tagged.Id, new List<int> { tag.Id });

        var (items, total) = await repo.GetAllAsync(user.Id, new ContactFilterParams { TagId = tag.Id });

        total.Should().Be(1);
        items.Should().ContainSingle().Which.FirstName.Should().Be("Tagged");
    }

    [Theory]
    [InlineData("firstname", "asc", new[] { "Alice", "Bob", "Charlie" })]
    [InlineData("firstname", "desc", new[] { "Charlie", "Bob", "Alice" })]
    [InlineData("lastname", "asc", new[] { "Bob", "Alice", "Charlie" })]
    [InlineData("lastname", "desc", new[] { "Charlie", "Alice", "Bob" })]
    [InlineData("email", "asc", new[] { "Alice", "Bob", "Charlie" })]
    [InlineData("email", "desc", new[] { "Charlie", "Bob", "Alice" })]
    public async Task GetAllAsync_SortsByField_InGivenDirection(
        string sort, string direction, string[] expectedOrder)
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Charlie", lastName: "Carter"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Alice", lastName: "Brown"));
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Bob", lastName: "Anderson"));

        var (items, _) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Sort = sort, Direction = direction });

        items.Select(c => c.FirstName).Should().ContainInOrder(expectedOrder);
    }

    [Fact]
    public async Task GetAllAsync_SortsByLastContacted_AscAndDesc()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);

        var older = TestDataFactory.CreateContact(user.Id, firstName: "Older");
        older.LastContactedAt = DateTime.UtcNow.AddDays(-10);
        var newer = TestDataFactory.CreateContact(user.Id, firstName: "Newer");
        newer.LastContactedAt = DateTime.UtcNow;
        await repo.CreateAsync(older);
        await repo.CreateAsync(newer);

        var (asc, _) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Sort = "lastcontacted", Direction = "asc" });
        var (desc, _) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Sort = "lastcontacted", Direction = "desc" });

        asc.Select(c => c.FirstName).Should().ContainInOrder("Older", "Newer");
        desc.Select(c => c.FirstName).Should().ContainInOrder("Newer", "Older");
    }

    [Fact]
    public async Task GetAllAsync_DefaultSort_OrdersByCreatedAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "First"));
        await Task.Delay(10);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Second"));
        await Task.Delay(10);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: "Third"));

        var (items, _) = await repo.GetAllAsync(user.Id, new ContactFilterParams());

        items.Select(c => c.FirstName).Should().ContainInOrder("Third", "Second", "First");
    }

    [Fact]
    public async Task GetAllAsync_AppliesPagination()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        for (int i = 1; i <= 5; i++)
        {
            await repo.CreateAsync(TestDataFactory.CreateContact(user.Id, firstName: $"Contact{i}"));
        }

        var (page1, total) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Page = 1, PerPage = 2, Sort = "firstname", Direction = "asc" });
        var (page2, _) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Page = 2, PerPage = 2, Sort = "firstname", Direction = "asc" });
        var (page3, _) = await repo.GetAllAsync(
            user.Id, new ContactFilterParams { Page = 3, PerPage = 2, Sort = "firstname", Direction = "asc" });

        total.Should().Be(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_OnlyReturnsContactsForGivenUser()
    {
        using var db = TestDbContext.Create();
        var userA = await new UserRepository(db).CreateAsync(
            TestDataFactory.CreateUser(name: "A", email: "a@example.com"));
        var userB = await new UserRepository(db).CreateAsync(
            TestDataFactory.CreateUser(name: "B", email: "b@example.com"));
        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(userA.Id, firstName: "AContact"));
        await repo.CreateAsync(TestDataFactory.CreateContact(userB.Id, firstName: "BContact"));

        var (items, total) = await repo.GetAllAsync(userA.Id, new ContactFilterParams());

        total.Should().Be(1);
        items.Should().ContainSingle().Which.FirstName.Should().Be("AContact");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        contact.FirstName = "Renamed";
        contact.Title = "CTO";
        await repo.UpdateAsync(contact);

        var fetched = await repo.GetByIdAsync(contact.Id);
        fetched!.FirstName.Should().Be("Renamed");
        fetched.Title.Should().Be("CTO");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenContactExists()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        (await repo.ExistsAsync(contact.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenContactDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new ContactRepository(db);

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task SyncTagsAsync_DoesNothing_WhenContactDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var tag = await new TagRepository(db).CreateAsync(TestDataFactory.CreateTag());
        var repo = new ContactRepository(db);

        // Should not throw.
        await repo.SyncTagsAsync(9999, new List<int> { tag.Id });

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task SyncTagsAsync_WithEmptyList_ClearsAllTags()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var tag = await new TagRepository(db).CreateAsync(TestDataFactory.CreateTag());
        var repo = new ContactRepository(db);
        var contact = await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));
        await repo.SyncTagsAsync(contact.Id, new List<int> { tag.Id });

        await repo.SyncTagsAsync(contact.Id, new List<int>());

        var fetched = await repo.GetByIdAsync(contact.Id);
        fetched!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenContactDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var repo = new ContactRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateContact(user.Id));

        await repo.DeleteAsync(9999);

        (await repo.GetCountAsync(user.Id)).Should().Be(1);
    }
}
