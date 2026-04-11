using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class DealRepositoryTests
{
    private async Task<(int UserId, int ContactId)> SeedUserAndContact(
        Infrastructure.Data.AppDbContext db)
    {
        var userRepo = new UserRepository(db);
        var user = await userRepo.CreateAsync(TestDataFactory.CreateUser());
        var contactRepo = new ContactRepository(db);
        var contact = await contactRepo.CreateAsync(TestDataFactory.CreateContact(user.Id));
        return (user.Id, contact.Id);
    }

    [Fact]
    public async Task CreateAsync_AddsDealToDatabase()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);

        var deal = TestDataFactory.CreateDeal(userId, contactId, value: 50000m);
        var result = await repo.CreateAsync(deal);

        Assert.True(result.Id > 0);
        Assert.Equal(50000m, result.Value);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStage()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);

        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Lead Deal", stage: DealStage.Lead));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Won Deal", stage: DealStage.Won));

        var filter = new DealFilterParams { Stage = "Won" };
        var (items, total) = await repo.GetAllAsync(userId, filter);

        Assert.Single(items);
        Assert.Equal("Won Deal", items[0].Title);
    }

    [Fact]
    public async Task GetWonRevenueAsync_SumsWonDeals()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);

        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 10000m, stage: DealStage.Won));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 25000m, stage: DealStage.Won));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 50000m, stage: DealStage.Lead));

        var revenue = await repo.GetWonRevenueAsync(userId);

        Assert.Equal(35000m, revenue);
    }

    [Fact]
    public async Task GetByStageAsync_ReturnsDealsInStage()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);

        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "A", stage: DealStage.Proposal));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "B", stage: DealStage.Proposal));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "C", stage: DealStage.Lead));

        var result = await repo.GetByStageAsync(userId, DealStage.Proposal);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetStageStatsAsync_ReturnsCorrectStats()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);

        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 10000m, stage: DealStage.Lead));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 20000m, stage: DealStage.Lead));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 50000m, stage: DealStage.Won));

        var stats = await repo.GetStageStatsAsync(userId);

        Assert.Equal(2, stats[DealStage.Lead].Count);
        Assert.Equal(30000m, stats[DealStage.Lead].Total);
        Assert.Equal(1, stats[DealStage.Won].Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDeal_WithContactAndCompanyIncluded()
    {
        using var db = TestDbContext.Create();
        var user = await new UserRepository(db).CreateAsync(TestDataFactory.CreateUser());
        var company = await new CompanyRepository(db).CreateAsync(TestDataFactory.CreateCompany(user.Id));
        var contact = await new ContactRepository(db).CreateAsync(
            TestDataFactory.CreateContact(user.Id, companyId: company.Id));
        var repo = new DealRepository(db);
        var deal = await repo.CreateAsync(
            TestDataFactory.CreateDeal(user.Id, contact.Id, company.Id, "With Includes"));

        var result = await repo.GetByIdAsync(deal.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("With Includes");
        result.Contact.Should().NotBeNull();
        result.Company.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenDealDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new DealRepository(db);

        var result = await repo.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_IncludesActivitiesAndInsights()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var dealRepo = new DealRepository(db);
        var deal = await dealRepo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId));
        await new ActivityRepository(db).CreateAsync(
            TestDataFactory.CreateActivity(userId, dealId: deal.Id));
        await new AiInsightRepository(db).CreateAsync(new Core.Entities.AiInsight
        {
            DealId = deal.Id,
            Insight = "Test insight",
            GeneratedAt = DateTime.UtcNow
        });

        var result = await dealRepo.GetByIdWithDetailsAsync(deal.Id);

        result.Should().NotBeNull();
        result!.Activities.Should().ContainSingle();
        result.AiInsights.Should().ContainSingle();
        result.AiInsights.First().Insight.Should().Be("Test insight");
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenNotFound()
    {
        using var db = TestDbContext.Create();
        var repo = new DealRepository(db);

        var result = await repo.GetByIdWithDetailsAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySearch_OnTitle()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Big Sale"));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Small Order"));

        var (items, total) = await repo.GetAllAsync(
            userId, new DealFilterParams { Search = "big" });

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Title.Should().Be("Big Sale");
    }

    [Fact]
    public async Task GetAllAsync_InvalidStageFilter_IsIgnored()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "A"));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "B"));

        var (items, total) = await repo.GetAllAsync(
            userId, new DealFilterParams { Stage = "NotARealStage" });

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_EmptyStageFilter_IsIgnored()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "X"));

        var (items, total) = await repo.GetAllAsync(
            userId, new DealFilterParams { Stage = "   " });

        total.Should().Be(1);
        items.Should().ContainSingle();
    }

    [Theory]
    [InlineData("title", "asc", new[] { "Alpha", "Bravo", "Charlie" })]
    [InlineData("title", "desc", new[] { "Charlie", "Bravo", "Alpha" })]
    public async Task GetAllAsync_SortsByTitle(string sort, string direction, string[] expectedOrder)
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Charlie"));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Alpha"));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Bravo"));

        var (items, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Sort = sort, Direction = direction });

        items.Select(d => d.Title).Should().ContainInOrder(expectedOrder);
    }

    [Fact]
    public async Task GetAllAsync_SortsByValue_Ascending()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 5000m));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 10000m));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 1000m));

        var (items, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Sort = "value", Direction = "asc" });

        items.Select(d => d.Value).Should().ContainInOrder(1000m, 5000m, 10000m);
    }

    [Fact]
    public async Task GetAllAsync_SortsByValue_Descending()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 5000m));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 10000m));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 1000m));

        var (items, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Sort = "value", Direction = "desc" });

        items.Select(d => d.Value).Should().ContainInOrder(10000m, 5000m, 1000m);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task GetAllAsync_SortsByStage(string direction)
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, stage: DealStage.Won));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, stage: DealStage.Lead));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, stage: DealStage.Proposal));

        var (items, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Sort = "stage", Direction = direction });

        items.Should().HaveCount(3);
        if (direction == "asc")
        {
            items.Select(d => d.Stage).Should().BeInAscendingOrder();
        }
        else
        {
            items.Select(d => d.Stage).Should().BeInDescendingOrder();
        }
    }

    [Fact]
    public async Task GetAllAsync_DefaultSort_OrdersByCreatedAtDescending()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "First"));
        await Task.Delay(10);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Second"));

        var (items, _) = await repo.GetAllAsync(userId, new DealFilterParams());

        items.Select(d => d.Title).Should().ContainInOrder("Second", "First");
    }

    [Fact]
    public async Task GetAllAsync_AppliesPagination()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        for (int i = 1; i <= 5; i++)
        {
            await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: $"Deal{i}"));
        }

        var (page1, total) = await repo.GetAllAsync(
            userId, new DealFilterParams { Page = 1, PerPage = 2, Sort = "title", Direction = "asc" });
        var (page2, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Page = 2, PerPage = 2, Sort = "title", Direction = "asc" });
        var (page3, _) = await repo.GetAllAsync(
            userId, new DealFilterParams { Page = 3, PerPage = 2, Sort = "title", Direction = "asc" });

        total.Should().Be(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_OnlyReturnsDealsForGivenUser()
    {
        using var db = TestDbContext.Create();
        var userA = await new UserRepository(db).CreateAsync(
            TestDataFactory.CreateUser(name: "A", email: "a@example.com"));
        var userB = await new UserRepository(db).CreateAsync(
            TestDataFactory.CreateUser(name: "B", email: "b@example.com"));
        var contactA = await new ContactRepository(db).CreateAsync(
            TestDataFactory.CreateContact(userA.Id, firstName: "AC"));
        var contactB = await new ContactRepository(db).CreateAsync(
            TestDataFactory.CreateContact(userB.Id, firstName: "BC"));
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userA.Id, contactA.Id, title: "ADeal"));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userB.Id, contactB.Id, title: "BDeal"));

        var (items, total) = await repo.GetAllAsync(userA.Id, new DealFilterParams());

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Title.Should().Be("ADeal");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        var deal = await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, title: "Original"));

        deal.Title = "Updated";
        deal.Value = 99999m;
        deal.Stage = DealStage.Won;
        await repo.UpdateAsync(deal);

        var fetched = await repo.GetByIdAsync(deal.Id);
        fetched!.Title.Should().Be("Updated");
        fetched.Value.Should().Be(99999m);
        fetched.Stage.Should().Be(DealStage.Won);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDeal()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        var deal = await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId));

        await repo.DeleteAsync(deal.Id);

        (await repo.ExistsAsync(deal.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenDealDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId));

        await repo.DeleteAsync(9999);

        var (items, _) = await repo.GetAllAsync(userId, new DealFilterParams());
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenDealExists()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        var deal = await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId));

        (await repo.ExistsAsync(deal.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenDealDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new DealRepository(db);

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task GetByStageAsync_OrdersByValueDescending()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 1000m, stage: DealStage.Proposal));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 5000m, stage: DealStage.Proposal));
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 3000m, stage: DealStage.Proposal));

        var result = await repo.GetByStageAsync(userId, DealStage.Proposal);

        result.Select(d => d.Value).Should().ContainInOrder(5000m, 3000m, 1000m);
    }

    [Fact]
    public async Task GetByStageAsync_ReturnsEmpty_WhenNoMatches()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, stage: DealStage.Lead));

        var result = await repo.GetByStageAsync(userId, DealStage.Won);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWonRevenueAsync_ReturnsZero_WhenNoWonDeals()
    {
        using var db = TestDbContext.Create();
        var (userId, contactId) = await SeedUserAndContact(db);
        var repo = new DealRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateDeal(userId, contactId, value: 10000m, stage: DealStage.Lead));

        var revenue = await repo.GetWonRevenueAsync(userId);

        revenue.Should().Be(0m);
    }
}
