using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class TagRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsTagToDatabase()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);

        var result = await repo.CreateAsync(TestDataFactory.CreateTag("VIP", "#FF0000"));

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("VIP");
        result.Color.Should().Be("#FF0000");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTag_WhenExists()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var created = await repo.CreateAsync(TestDataFactory.CreateTag("Lead"));

        var result = await repo.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lead");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);

        var result = await repo.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTagsOrderedByName()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Charlie"));
        await repo.CreateAsync(TestDataFactory.CreateTag("Alpha"));
        await repo.CreateAsync(TestDataFactory.CreateTag("Bravo"));

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(t => t.Name).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoTags()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);

        var result = await repo.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesTag()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var tag = await repo.CreateAsync(TestDataFactory.CreateTag("Old"));

        tag.Name = "Renamed";
        tag.Color = "#0000FF";
        await repo.UpdateAsync(tag);

        var result = await repo.GetByIdAsync(tag.Id);
        result!.Name.Should().Be("Renamed");
        result.Color.Should().Be("#0000FF");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTag()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var tag = await repo.CreateAsync(TestDataFactory.CreateTag());

        await repo.DeleteAsync(tag.Id);

        (await repo.GetByIdAsync(tag.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenTagDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Stays"));

        await repo.DeleteAsync(9999);

        (await repo.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenTagExists()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var tag = await repo.CreateAsync(TestDataFactory.CreateTag());

        (await repo.ExistsAsync(tag.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenTagDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);

        (await repo.ExistsAsync(9999)).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsTrue_WhenNameInUse()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Hot"));

        (await repo.NameExistsAsync("Hot")).Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_ReturnsFalse_WhenNameNotInUse()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);

        (await repo.NameExistsAsync("Anything")).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_IsCaseSensitive()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Hot"));

        // EF InMemory does ordinal comparisons by default, so "hot" != "Hot".
        (await repo.NameExistsAsync("hot")).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ExcludesSelf_WhenExcludeIdProvided()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var tag = await repo.CreateAsync(TestDataFactory.CreateTag("Renaming"));

        // When updating this tag, we shouldn't see the same name as a conflict.
        (await repo.NameExistsAsync("Renaming", tag.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_StillFindsConflict_WhenOtherTagHasSameName()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var tagA = await repo.CreateAsync(TestDataFactory.CreateTag("SameName"));
        var tagB = await repo.CreateAsync(TestDataFactory.CreateTag("Different"));

        // Pretend we're trying to rename tagB to "SameName".
        (await repo.NameExistsAsync("SameName", tagB.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsMatchingTags()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var t1 = await repo.CreateAsync(TestDataFactory.CreateTag("One"));
        var t2 = await repo.CreateAsync(TestDataFactory.CreateTag("Two"));
        var t3 = await repo.CreateAsync(TestDataFactory.CreateTag("Three"));

        var result = await repo.GetByIdsAsync(new List<int> { t1.Id, t3.Id });

        result.Should().HaveCount(2);
        result.Select(t => t.Name).Should().BeEquivalentTo(new[] { "One", "Three" });
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsEmpty_WhenNoIdsMatch()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Real"));

        var result = await repo.GetByIdsAsync(new List<int> { 9998, 9999 });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsEmpty_WhenIdListIsEmpty()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateTag("Real"));

        var result = await repo.GetByIdsAsync(new List<int>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_IgnoresMissingIds()
    {
        using var db = TestDbContext.Create();
        var repo = new TagRepository(db);
        var real = await repo.CreateAsync(TestDataFactory.CreateTag("Real"));

        var result = await repo.GetByIdsAsync(new List<int> { real.Id, 9999 });

        result.Should().ContainSingle();
        result.First().Id.Should().Be(real.Id);
    }
}
