using SunroomCrm.Core.Entities;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class AiInsightRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsInsightToDatabase()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new AiInsightRepository(db);

        var result = await repo.CreateAsync(new AiInsight
        {
            DealId = deal.Id,
            Insight = "This deal is hot."
        });

        result.Id.Should().BeGreaterThan(0);
        result.Insight.Should().Be("This deal is hot.");
        result.DealId.Should().Be(deal.Id);
    }

    [Fact]
    public async Task CreateAsync_DefaultsGeneratedAtToUtcNow()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new AiInsightRepository(db);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await repo.CreateAsync(new AiInsight
        {
            DealId = deal.Id,
            Insight = "Auto timestamp"
        });

        result.GeneratedAt.Should().BeOnOrAfter(before);
        result.GeneratedAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetForDealAsync_ReturnsInsightsForDeal()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var dealA = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "A");
        var dealB = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id, title: "B");
        var repo = new AiInsightRepository(db);

        await repo.CreateAsync(new AiInsight { DealId = dealA.Id, Insight = "A1" });
        await repo.CreateAsync(new AiInsight { DealId = dealA.Id, Insight = "A2" });
        await repo.CreateAsync(new AiInsight { DealId = dealB.Id, Insight = "B1" });

        var result = await repo.GetForDealAsync(dealA.Id);

        result.Should().HaveCount(2);
        result.Select(i => i.Insight).Should().BeEquivalentTo(new[] { "A1", "A2" });
    }

    [Fact]
    public async Task GetForDealAsync_OrdersByGeneratedAtDescending()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new AiInsightRepository(db);

        await repo.CreateAsync(new AiInsight
        {
            DealId = deal.Id,
            Insight = "Older",
            GeneratedAt = DateTime.UtcNow.AddDays(-2)
        });
        await repo.CreateAsync(new AiInsight
        {
            DealId = deal.Id,
            Insight = "Newer",
            GeneratedAt = DateTime.UtcNow
        });
        await repo.CreateAsync(new AiInsight
        {
            DealId = deal.Id,
            Insight = "Middle",
            GeneratedAt = DateTime.UtcNow.AddDays(-1)
        });

        var result = await repo.GetForDealAsync(deal.Id);

        result.Select(i => i.Insight).Should().ContainInOrder("Newer", "Middle", "Older");
    }

    [Fact]
    public async Task GetForDealAsync_ReturnsEmpty_WhenNoInsights()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = await TestSeedHelper.SeedContactAsync(db, user.Id);
        var deal = await TestSeedHelper.SeedDealAsync(db, user.Id, contact.Id);
        var repo = new AiInsightRepository(db);

        var result = await repo.GetForDealAsync(deal.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForDealAsync_ReturnsEmpty_WhenDealDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new AiInsightRepository(db);

        var result = await repo.GetForDealAsync(9999);

        result.Should().BeEmpty();
    }
}
