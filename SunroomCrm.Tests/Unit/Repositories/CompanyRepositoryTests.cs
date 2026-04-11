using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class CompanyRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsCompanyToDatabase()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);

        var result = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Acme"));

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Acme");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCompany_WhenExists()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var company = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Acme"));

        var result = await repo.GetByIdAsync(company.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Acme");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new CompanyRepository(db);

        var result = await repo.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_LoadsContactsAndDealsWithContact()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var company = await TestSeedHelper.SeedCompanyAsync(db, user.Id, "Detailed Co");
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id, company.Id, "Alice", "Anderson");
        await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, company.Id, "Big Deal");

        var repo = new CompanyRepository(db);
        var result = await repo.GetByIdWithDetailsAsync(company.Id);

        result.Should().NotBeNull();
        result!.Contacts.Should().HaveCount(1);
        result.Contacts.First().FirstName.Should().Be("Alice");
        result.Deals.Should().HaveCount(1);
        result.Deals.First().Title.Should().Be("Big Deal");
        result.Deals.First().Contact.Should().NotBeNull();
        result.Deals.First().Contact!.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new CompanyRepository(db);

        var result = await repo.GetByIdWithDetailsAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyOwnedCompanies()
    {
        using var db = TestDbContext.Create();
        var owner = await TestSeedHelper.SeedUserAsync(db, email: "owner@example.com");
        var stranger = await TestSeedHelper.SeedUserAsync(db, email: "stranger@example.com");
        var repo = new CompanyRepository(db);

        await repo.CreateAsync(TestDataFactory.CreateCompany(owner.Id, "Mine"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(stranger.Id, "NotMine"));

        var (items, total) = await repo.GetAllAsync(owner.Id, null, new PaginationParams());

        total.Should().Be(1);
        items.Should().ContainSingle(c => c.Name == "Mine");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenUserHasNoCompanies()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);

        var (items, total) = await repo.GetAllAsync(user.Id, null, new PaginationParams());

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearchOnName()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Acme Corp"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Globex"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Initech"));

        var (items, total) = await repo.GetAllAsync(user.Id, "acme", new PaginationParams());

        total.Should().Be(1);
        items.Should().ContainSingle(c => c.Name == "Acme Corp");
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearchOnIndustry()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
        {
            UserId = user.Id, Name = "Tech Co", Industry = "Software", City = "Austin"
        });
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
        {
            UserId = user.Id, Name = "Food Co", Industry = "Restaurants", City = "Dallas"
        });

        var (items, total) = await repo.GetAllAsync(user.Id, "software", new PaginationParams());

        total.Should().Be(1);
        items.Should().ContainSingle(c => c.Name == "Tech Co");
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearchOnCity()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
        {
            UserId = user.Id, Name = "Alpha", Industry = "Tech", City = "Austin"
        });
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
        {
            UserId = user.Id, Name = "Beta", Industry = "Tech", City = "Boston"
        });

        var (items, total) = await repo.GetAllAsync(user.Id, "boston", new PaginationParams());

        total.Should().Be(1);
        items.Should().ContainSingle(c => c.Name == "Beta");
    }

    [Fact]
    public async Task GetAllAsync_SearchIgnoresNullIndustryAndCity()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
        {
            UserId = user.Id, Name = "Sparse", Industry = null, City = null
        });

        var (items, total) = await repo.GetAllAsync(user.Id, "anything", new PaginationParams());

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_WhitespaceSearchReturnsAll()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Acme"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Globex"));

        var (items, total) = await repo.GetAllAsync(user.Id, "   ", new PaginationParams());

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("name", "asc", new[] { "Alpha", "Bravo", "Charlie" })]
    [InlineData("name", "desc", new[] { "Charlie", "Bravo", "Alpha" })]
    public async Task GetAllAsync_SortsByName(string sort, string direction, string[] expected)
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Bravo"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Charlie"));
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Alpha"));

        var (items, _) = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Sort = sort, Direction = direction });

        items.Select(c => c.Name).Should().ContainInOrder(expected);
    }

    [Theory]
    [InlineData("industry", "asc", new[] { "Aerospace", "Banking", "Construction" })]
    [InlineData("industry", "desc", new[] { "Construction", "Banking", "Aerospace" })]
    public async Task GetAllAsync_SortsByIndustry(string sort, string direction, string[] expected)
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
            { UserId = user.Id, Name = "B Co", Industry = "Banking" });
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
            { UserId = user.Id, Name = "C Co", Industry = "Construction" });
        await repo.CreateAsync(new SunroomCrm.Core.Entities.Company
            { UserId = user.Id, Name = "A Co", Industry = "Aerospace" });

        var (items, _) = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Sort = sort, Direction = direction });

        items.Select(c => c.Industry).Should().ContainInOrder(expected);
    }

    [Fact]
    public async Task GetAllAsync_DefaultSortIsCreatedAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var first = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "First"));
        // Force a clearly later CreatedAt to dodge in-memory ordering ties.
        first.CreatedAt = DateTime.UtcNow.AddDays(-2);
        await repo.UpdateAsync(first);

        var second = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Second"));
        second.CreatedAt = DateTime.UtcNow.AddDays(-1);
        await repo.UpdateAsync(second);

        var third = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Third"));
        third.CreatedAt = DateTime.UtcNow;
        await repo.UpdateAsync(third);

        var (items, _) = await repo.GetAllAsync(user.Id, null, new PaginationParams());

        items.Select(c => c.Name).Should().ContainInOrder("Third", "Second", "First");
    }

    [Fact]
    public async Task GetAllAsync_UnknownSortFallsBackToCreatedAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var first = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "First"));
        first.CreatedAt = DateTime.UtcNow.AddHours(-2);
        await repo.UpdateAsync(first);

        var second = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Second"));
        second.CreatedAt = DateTime.UtcNow;
        await repo.UpdateAsync(second);

        var (items, _) = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Sort = "bogus", Direction = "asc" });

        items.Select(c => c.Name).Should().ContainInOrder("Second", "First");
    }

    [Fact]
    public async Task GetAllAsync_PaginationReturnsCorrectPage()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        for (int i = 1; i <= 5; i++)
            await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, $"Co{i:00}"));

        var pageOne = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Page = 1, PerPage = 2, Sort = "name", Direction = "asc" });
        var pageTwo = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Page = 2, PerPage = 2, Sort = "name", Direction = "asc" });
        var pageThree = await repo.GetAllAsync(
            user.Id, null, new PaginationParams { Page = 3, PerPage = 2, Sort = "name", Direction = "asc" });

        pageOne.Total.Should().Be(5);
        pageOne.Items.Should().HaveCount(2);
        pageOne.Items.Select(c => c.Name).Should().ContainInOrder("Co01", "Co02");
        pageTwo.Items.Select(c => c.Name).Should().ContainInOrder("Co03", "Co04");
        pageThree.Items.Should().ContainSingle().Which.Name.Should().Be("Co05");
    }

    [Fact]
    public async Task GetAllAsync_IncludesContactsAndDeals()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var company = await TestSeedHelper.SeedCompanyAsync(db, user.Id);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id, company.Id);
        await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, company.Id);

        var repo = new CompanyRepository(db);
        var (items, _) = await repo.GetAllAsync(user.Id, null, new PaginationParams());

        items.Should().ContainSingle();
        items.First().Contacts.Should().HaveCount(1);
        items.First().Deals.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesCompany()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var company = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Old Name"));

        company.Name = "New Name";
        company.Industry = "Healthcare";
        await repo.UpdateAsync(company);

        var result = await repo.GetByIdAsync(company.Id);
        result!.Name.Should().Be("New Name");
        result.Industry.Should().Be("Healthcare");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCompany()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var company = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id));

        await repo.DeleteAsync(company.Id);

        (await repo.GetByIdAsync(company.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenCompanyDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id, "Stays"));

        await repo.DeleteAsync(9999);

        var (items, _) = await repo.GetAllAsync(user.Id, null, new PaginationParams());
        items.Should().ContainSingle();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenCompanyExists()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var repo = new CompanyRepository(db);
        var company = await repo.CreateAsync(TestDataFactory.CreateCompany(user.Id));

        (await repo.ExistsAsync(company.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenCompanyDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new CompanyRepository(db);

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }
}
