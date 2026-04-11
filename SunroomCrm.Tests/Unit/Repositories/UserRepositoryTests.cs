using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Repositories;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task CreateAsync_AddsUserToDatabase()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = TestDataFactory.CreateUser();

        var result = await repo.CreateAsync(user);

        Assert.True(result.Id > 0);
        Assert.Equal("Test User", result.Name);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsUser_WhenExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = TestDataFactory.CreateUser(email: "find@example.com");
        await repo.CreateAsync(user);

        var result = await repo.GetByEmailAsync("find@example.com");

        Assert.NotNull(result);
        Assert.Equal("find@example.com", result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);

        var result = await repo.GetByEmailAsync("nobody@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task EmailExistsAsync_ReturnsTrue_WhenExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateUser(email: "exists@example.com"));

        Assert.True(await repo.EmailExistsAsync("exists@example.com"));
    }

    [Fact]
    public async Task EmailExistsAsync_ReturnsFalse_WhenNotExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);

        Assert.False(await repo.EmailExistsAsync("nope@example.com"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesUser()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = await repo.CreateAsync(TestDataFactory.CreateUser());

        await repo.DeleteAsync(user.Id);

        Assert.Null(await repo.GetByIdAsync(user.Id));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateUser(name: "Alice", email: "alice@example.com"));
        await repo.CreateAsync(TestDataFactory.CreateUser(name: "Bob", email: "bob@example.com"));

        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesUser()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = await repo.CreateAsync(TestDataFactory.CreateUser());

        user.Name = "Updated Name";
        user.Role = UserRole.Admin;
        await repo.UpdateAsync(user);

        var result = await repo.GetByIdAsync(user.Id);
        Assert.Equal("Updated Name", result!.Name);
        Assert.Equal(UserRole.Admin, result.Role);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);

        var result = await repo.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = await repo.CreateAsync(TestDataFactory.CreateUser(name: "FindMe"));

        var result = await repo.GetByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("FindMe");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenUserExists()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        var user = await repo.CreateAsync(TestDataFactory.CreateUser());

        var exists = await repo.ExistsAsync(user.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);

        var exists = await repo.ExistsAsync(9999);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenUserDoesNotExist()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateUser(email: "stays@example.com"));

        // Should not throw and should not affect existing data.
        await repo.DeleteAsync(9999);

        var all = await repo.GetAllAsync();
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByName_Alphabetically()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);
        await repo.CreateAsync(TestDataFactory.CreateUser(name: "Charlie", email: "c@example.com"));
        await repo.CreateAsync(TestDataFactory.CreateUser(name: "Alice", email: "a@example.com"));
        await repo.CreateAsync(TestDataFactory.CreateUser(name: "Bob", email: "b@example.com"));

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(u => u.Name).Should().ContainInOrder("Alice", "Bob", "Charlie");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoUsers()
    {
        using var db = TestDbContext.Create();
        var repo = new UserRepository(db);

        var result = await repo.GetAllAsync();

        result.Should().BeEmpty();
    }
}
