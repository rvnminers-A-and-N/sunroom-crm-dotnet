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
}
