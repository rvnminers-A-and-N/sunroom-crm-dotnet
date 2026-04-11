using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_users.Object);
    }

    private static User MakeUser(int id = 1, string name = "User", string email = "u@example.com", UserRole role = UserRole.User)
        => new()
        {
            Id = id,
            Name = name,
            Email = email,
            Role = role,
            CreatedAt = new DateTime(2024, 1, 1)
        };

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsOkWithMappedUserDtos()
    {
        var users = new List<User>
        {
            MakeUser(1, "Alice", "alice@example.com", UserRole.Admin),
            MakeUser(2, "Bob", "bob@example.com", UserRole.User)
        };
        _users.Setup(u => u.GetAllAsync()).ReturnsAsync(users);

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<UserDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].Name.Should().Be("Alice");
        dtos[0].Role.Should().Be("Admin");
        dtos[1].Name.Should().Be("Bob");
        dtos[1].Role.Should().Be("User");
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoUsers()
    {
        _users.Setup(u => u.GetAllAsync()).ReturnsAsync(new List<User>());

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<UserDto>>().Subject;
        dtos.Should().BeEmpty();
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenUserDoesNotExist()
    {
        _users.Setup(u => u.GetByIdAsync(99)).ReturnsAsync((User?)null);

        var result = await _controller.GetById(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOkWithUserDto_WhenFound()
    {
        var user = MakeUser(5, "Charlie", "c@example.com", UserRole.Manager);
        user.AvatarUrl = "https://example.com/c.png";
        _users.Setup(u => u.GetByIdAsync(5)).ReturnsAsync(user);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<UserDto>().Subject;
        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Charlie");
        dto.Email.Should().Be("c@example.com");
        dto.Role.Should().Be("Manager");
        dto.AvatarUrl.Should().Be("https://example.com/c.png");
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenUserDoesNotExist()
    {
        _users.Setup(u => u.GetByIdAsync(42)).ReturnsAsync((User?)null);

        var result = await _controller.Update(42, new UpdateUserRequest { Name = "X" });

        result.Should().BeOfType<NotFoundResult>();
        _users.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_UpdatesNameWhenProvided()
    {
        var user = MakeUser(1, "Old Name");
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await _controller.Update(1, new UpdateUserRequest { Name = "New Name" });

        user.Name.Should().Be("New Name");
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<UserDto>().Subject;
        dto.Name.Should().Be("New Name");
        _users.Verify(u => u.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Update_DoesNotChangeName_WhenNameIsNull()
    {
        var user = MakeUser(1, "Original");
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { Name = null });

        user.Name.Should().Be("Original");
    }

    [Fact]
    public async Task Update_DoesNotChangeName_WhenNameIsWhitespace()
    {
        var user = MakeUser(1, "Original");
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { Name = "   " });

        user.Name.Should().Be("Original");
    }

    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Manager", UserRole.Manager)]
    [InlineData("User", UserRole.User)]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("MANAGER", UserRole.Manager)]
    public async Task Update_ParsesRoleCaseInsensitively(string roleString, UserRole expected)
    {
        var user = MakeUser(1, role: UserRole.User);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { Role = roleString });

        user.Role.Should().Be(expected);
    }

    [Fact]
    public async Task Update_DoesNotChangeRole_WhenRoleIsInvalid()
    {
        var user = MakeUser(1, role: UserRole.Manager);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { Role = "Superuser" });

        user.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public async Task Update_DoesNotChangeRole_WhenRoleIsNull()
    {
        var user = MakeUser(1, role: UserRole.Admin);
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { Role = null });

        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Update_UpdatesAvatarUrl_WhenProvided()
    {
        var user = MakeUser(1);
        user.AvatarUrl = "https://old.com/a.png";
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { AvatarUrl = "https://new.com/a.png" });

        user.AvatarUrl.Should().Be("https://new.com/a.png");
    }

    [Fact]
    public async Task Update_AllowsClearingAvatarUrl_WithEmptyString()
    {
        var user = MakeUser(1);
        user.AvatarUrl = "https://old.com/a.png";
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { AvatarUrl = "" });

        user.AvatarUrl.Should().Be("");
    }

    [Fact]
    public async Task Update_DoesNotChangeAvatarUrl_WhenNull()
    {
        var user = MakeUser(1);
        user.AvatarUrl = "https://kept.com/a.png";
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest { AvatarUrl = null });

        user.AvatarUrl.Should().Be("https://kept.com/a.png");
    }

    [Fact]
    public async Task Update_AppliesAllFields_WhenAllProvided()
    {
        var user = MakeUser(1, "Old", role: UserRole.User);
        user.AvatarUrl = "old";
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(user);

        await _controller.Update(1, new UpdateUserRequest
        {
            Name = "New",
            Role = "Admin",
            AvatarUrl = "new"
        });

        user.Name.Should().Be("New");
        user.Role.Should().Be(UserRole.Admin);
        user.AvatarUrl.Should().Be("new");
        _users.Verify(u => u.UpdateAsync(user), Times.Once);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenUserDoesNotExist()
    {
        _users.Setup(u => u.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _users.Verify(u => u.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _users.Setup(u => u.ExistsAsync(7)).ReturnsAsync(true);
        _users.Setup(u => u.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _users.Verify(u => u.DeleteAsync(7), Times.Once);
    }
}
