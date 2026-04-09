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
}
