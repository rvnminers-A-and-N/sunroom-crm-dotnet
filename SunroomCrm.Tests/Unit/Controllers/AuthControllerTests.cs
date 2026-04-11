using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Auth;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Tests.Unit.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(_users.Object, _tokens.Object);
    }

    private void SetAuthenticatedUser(int userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    // ---- Register ----

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailAlreadyExists()
    {
        _users.Setup(u => u.EmailExistsAsync("dup@example.com")).ReturnsAsync(true);
        var request = new RegisterRequest { Name = "Dup", Email = "dup@example.com", Password = "password123" };

        var result = await _controller.Register(request);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { message = "Email already registered." });
        _users.Verify(u => u.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_HashesPassword_BeforePersisting()
    {
        User? capturedUser = null;
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _users.Setup(u => u.CreateAsync(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u)
            .ReturnsAsync((User u) => u);
        _tokens.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("jwt-token");

        var request = new RegisterRequest { Name = "New", Email = "new@example.com", Password = "secret123" };

        await _controller.Register(request);

        capturedUser.Should().NotBeNull();
        capturedUser!.Password.Should().NotBe("secret123");
        BCrypt.Net.BCrypt.Verify("secret123", capturedUser.Password).Should().BeTrue();
    }

    [Fact]
    public async Task Register_AssignsUserRole()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _users.Setup(u => u.CreateAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
        _tokens.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("jwt");

        await _controller.Register(new RegisterRequest { Name = "N", Email = "n@n.com", Password = "password123" });

        _users.Verify(u => u.CreateAsync(It.Is<User>(usr => usr.Role == UserRole.User)), Times.Once);
    }

    [Fact]
    public async Task Register_ReturnsCreatedAtAction_WithAuthResponse()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _users.Setup(u => u.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => { u.Id = 7; return u; });
        _tokens.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("jwt-token-here");

        var result = await _controller.Register(new RegisterRequest
        {
            Name = "Alice", Email = "alice@example.com", Password = "password123"
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(AuthController.Me));
        var body = created.Value.Should().BeOfType<AuthResponse>().Subject;
        body.Token.Should().Be("jwt-token-here");
        body.User.Email.Should().Be("alice@example.com");
        body.User.Name.Should().Be("Alice");
        body.User.Role.Should().Be("User");
        body.User.Id.Should().Be(7);
    }

    // ---- Login ----

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
    {
        _users.Setup(u => u.GetByEmailAsync("nobody@example.com")).ReturnsAsync((User?)null);

        var result = await _controller.Login(new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "password123"
        });

        var unauth = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauth.Value.Should().BeEquivalentTo(new { message = "Invalid credentials." });
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIncorrect()
    {
        var user = new User
        {
            Id = 1,
            Email = "u@example.com",
            Name = "U",
            Password = BCrypt.Net.BCrypt.HashPassword("correct-password"),
            Role = UserRole.User
        };
        _users.Setup(u => u.GetByEmailAsync("u@example.com")).ReturnsAsync(user);

        var result = await _controller.Login(new LoginRequest
        {
            Email = "u@example.com",
            Password = "wrong-password"
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _tokens.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_ReturnsOkWithToken_WhenCredentialsAreValid()
    {
        var user = new User
        {
            Id = 5,
            Email = "good@example.com",
            Name = "Good",
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Manager
        };
        _users.Setup(u => u.GetByEmailAsync("good@example.com")).ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateToken(user)).Returns("login-jwt");

        var result = await _controller.Login(new LoginRequest
        {
            Email = "good@example.com",
            Password = "password123"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        body.Token.Should().Be("login-jwt");
        body.User.Id.Should().Be(5);
        body.User.Role.Should().Be("Manager");
    }

    // ---- Logout ----

    [Fact]
    public void Logout_ReturnsOkMessage()
    {
        var result = _controller.Logout();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { message = "Logged out successfully." });
    }

    // ---- Me ----

    [Fact]
    public async Task Me_ReturnsNotFound_WhenUserDoesNotExist()
    {
        SetAuthenticatedUser(42);
        _users.Setup(u => u.GetByIdAsync(42)).ReturnsAsync((User?)null);

        var result = await _controller.Me();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Me_ReturnsCurrentUserDto_WhenAuthenticated()
    {
        SetAuthenticatedUser(99);
        var user = new User
        {
            Id = 99,
            Name = "Alice",
            Email = "alice@example.com",
            Role = UserRole.Admin,
            AvatarUrl = "https://example.com/a.png",
            CreatedAt = new DateTime(2024, 1, 1)
        };
        _users.Setup(u => u.GetByIdAsync(99)).ReturnsAsync(user);

        var result = await _controller.Me();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<UserDto>().Subject;
        dto.Id.Should().Be(99);
        dto.Name.Should().Be("Alice");
        dto.Email.Should().Be("alice@example.com");
        dto.Role.Should().Be("Admin");
        dto.AvatarUrl.Should().Be("https://example.com/a.png");
        dto.CreatedAt.Should().Be(new DateTime(2024, 1, 1));
    }

    [Fact]
    public async Task Me_UsesNameIdentifierClaimToFindUser()
    {
        SetAuthenticatedUser(123);
        _users.Setup(u => u.GetByIdAsync(123))
            .ReturnsAsync(new User { Id = 123, Name = "X", Email = "x@x.com", Role = UserRole.User });

        await _controller.Me();

        _users.Verify(u => u.GetByIdAsync(123), Times.Once);
    }
}
