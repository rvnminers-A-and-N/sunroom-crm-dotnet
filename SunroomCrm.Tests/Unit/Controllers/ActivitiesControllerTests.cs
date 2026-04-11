using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class ActivitiesControllerTests
{
    private readonly Mock<IActivityRepository> _activities = new();
    private readonly ActivitiesController _controller;

    public ActivitiesControllerTests()
    {
        _controller = new ActivitiesController(_activities.Object);
        SetAuthenticatedUser(1);
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

    private static Activity MakeActivity(int id = 1, ActivityType type = ActivityType.Note)
        => new()
        {
            Id = id,
            UserId = 1,
            User = new User { Id = 1, Name = "Bob", Email = "b@b.com" },
            Type = type,
            Subject = "Test",
            Body = "Body",
            OccurredAt = new DateTime(2024, 1, 1),
            CreatedAt = new DateTime(2024, 1, 1)
        };

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsPaginatedResponse()
    {
        var activities = new List<Activity>
        {
            MakeActivity(1, ActivityType.Call),
            MakeActivity(2, ActivityType.Note)
        };
        activities[0].Contact = new Contact { Id = 5, FirstName = "Jane", LastName = "Doe" };
        activities[0].ContactId = 5;
        activities[0].Deal = new Deal { Id = 7, Title = "Big Deal" };
        activities[0].DealId = 7;

        _activities.Setup(a => a.GetAllAsync(1, It.IsAny<ActivityFilterParams>()))
            .ReturnsAsync((activities, 2));

        var result = await _controller.GetAll(new ActivityFilterParams { Page = 1, PerPage = 25 });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<ActivityDto>>().Subject;
        paged.Data.Should().HaveCount(2);
        paged.Data[0].Type.Should().Be("Call");
        paged.Data[0].ContactName.Should().Be("Jane Doe");
        paged.Data[0].DealTitle.Should().Be("Big Deal");
        paged.Meta.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_PassesFilterToRepository()
    {
        var filter = new ActivityFilterParams { Page = 1, PerPage = 10, ContactId = 5, DealId = 7, Type = "Call" };
        _activities.Setup(a => a.GetAllAsync(1, filter)).ReturnsAsync((new List<Activity>(), 0));

        await _controller.GetAll(filter);

        _activities.Verify(a => a.GetAllAsync(1, filter), Times.Once);
    }

    [Fact]
    public async Task GetAll_HandlesNullContactAndDeal()
    {
        var activity = MakeActivity(1);
        _activities.Setup(a => a.GetAllAsync(1, It.IsAny<ActivityFilterParams>()))
            .ReturnsAsync((new List<Activity> { activity }, 1));

        var result = await _controller.GetAll(new ActivityFilterParams());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<ActivityDto>>().Subject;
        paged.Data[0].ContactName.Should().BeNull();
        paged.Data[0].DealTitle.Should().BeNull();
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenActivityDoesNotExist()
    {
        _activities.Setup(a => a.GetByIdAsync(99)).ReturnsAsync((Activity?)null);

        var result = await _controller.GetById(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsActivityDto_WhenFound()
    {
        var activity = MakeActivity(5, ActivityType.Email);
        activity.AiSummary = "AI summary text";
        _activities.Setup(a => a.GetByIdAsync(5)).ReturnsAsync(activity);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ActivityDto>().Subject;
        dto.Id.Should().Be(5);
        dto.Type.Should().Be("Email");
        dto.AiSummary.Should().Be("AI summary text");
        dto.UserName.Should().Be("Bob");
    }

    // ---- Create ----

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTypeIsInvalid()
    {
        var result = await _controller.Create(new CreateActivityRequest
        {
            Type = "BogusType",
            Subject = "S"
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { message = "Invalid activity type." });
        _activities.Verify(a => a.CreateAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Theory]
    [InlineData("Note", ActivityType.Note)]
    [InlineData("call", ActivityType.Call)]
    [InlineData("EMAIL", ActivityType.Email)]
    [InlineData("Meeting", ActivityType.Meeting)]
    [InlineData("task", ActivityType.Task)]
    public async Task Create_ParsesTypeCaseInsensitively(string type, ActivityType expected)
    {
        _activities.Setup(a => a.CreateAsync(It.IsAny<Activity>()))
            .Callback<Activity>(a => a.Id = 100)
            .ReturnsAsync((Activity a) => a);
        _activities.Setup(a => a.GetByIdAsync(100)).ReturnsAsync(MakeActivity(100, expected));

        await _controller.Create(new CreateActivityRequest { Type = type, Subject = "S" });

        _activities.Verify(a => a.CreateAsync(It.Is<Activity>(act => act.Type == expected)), Times.Once);
    }

    [Fact]
    public async Task Create_PersistsActivityWithAuthenticatedUser()
    {
        SetAuthenticatedUser(7);
        _activities.Setup(a => a.CreateAsync(It.IsAny<Activity>()))
            .Callback<Activity>(a => a.Id = 100)
            .ReturnsAsync((Activity a) => a);
        _activities.Setup(a => a.GetByIdAsync(100)).ReturnsAsync(MakeActivity(100));

        var result = await _controller.Create(new CreateActivityRequest
        {
            Type = "Call",
            Subject = "Discovery",
            Body = "Notes",
            ContactId = 5,
            DealId = 7,
            OccurredAt = new DateTime(2024, 6, 1)
        });

        _activities.Verify(a => a.CreateAsync(It.Is<Activity>(act =>
            act.UserId == 7 &&
            act.Type == ActivityType.Call &&
            act.Subject == "Discovery" &&
            act.Body == "Notes" &&
            act.ContactId == 5 &&
            act.DealId == 7 &&
            act.OccurredAt == new DateTime(2024, 6, 1))), Times.Once);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues!["id"].Should().Be(100);
        created.Value.Should().BeOfType<ActivityDto>();
    }

    [Fact]
    public async Task Create_DefaultsOccurredAtToUtcNow_WhenNotProvided()
    {
        _activities.Setup(a => a.CreateAsync(It.IsAny<Activity>()))
            .Callback<Activity>(a => a.Id = 1)
            .ReturnsAsync((Activity a) => a);
        _activities.Setup(a => a.GetByIdAsync(1)).ReturnsAsync(MakeActivity(1));

        var before = DateTime.UtcNow.AddSeconds(-1);
        await _controller.Create(new CreateActivityRequest { Type = "Note", Subject = "S", OccurredAt = null });
        var after = DateTime.UtcNow.AddSeconds(1);

        _activities.Verify(a => a.CreateAsync(It.Is<Activity>(act =>
            act.OccurredAt >= before && act.OccurredAt <= after)), Times.Once);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenActivityDoesNotExist()
    {
        _activities.Setup(a => a.GetByIdAsync(99)).ReturnsAsync((Activity?)null);

        var result = await _controller.Update(99, new UpdateActivityRequest { Type = "Note", Subject = "S" });

        result.Should().BeOfType<NotFoundResult>();
        _activities.Verify(a => a.UpdateAsync(It.IsAny<Activity>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesAllFields()
    {
        var activity = MakeActivity(1, ActivityType.Note);
        _activities.Setup(a => a.GetByIdAsync(1)).ReturnsAsync(activity);

        await _controller.Update(1, new UpdateActivityRequest
        {
            Type = "Call",
            Subject = "New Subject",
            Body = "New Body",
            ContactId = 5,
            DealId = 7,
            OccurredAt = new DateTime(2024, 5, 1)
        });

        activity.Type.Should().Be(ActivityType.Call);
        activity.Subject.Should().Be("New Subject");
        activity.Body.Should().Be("New Body");
        activity.ContactId.Should().Be(5);
        activity.DealId.Should().Be(7);
        activity.OccurredAt.Should().Be(new DateTime(2024, 5, 1));
        _activities.Verify(a => a.UpdateAsync(activity), Times.Once);
    }

    [Fact]
    public async Task Update_LeavesTypeUnchanged_WhenTypeIsInvalid()
    {
        var activity = MakeActivity(1, ActivityType.Note);
        _activities.Setup(a => a.GetByIdAsync(1)).ReturnsAsync(activity);

        await _controller.Update(1, new UpdateActivityRequest
        {
            Type = "BogusType",
            Subject = "S"
        });

        activity.Type.Should().Be(ActivityType.Note);
    }

    [Fact]
    public async Task Update_KeepsExistingOccurredAt_WhenOccurredAtIsNull()
    {
        var original = new DateTime(2024, 1, 1);
        var activity = MakeActivity(1);
        activity.OccurredAt = original;
        _activities.Setup(a => a.GetByIdAsync(1)).ReturnsAsync(activity);

        await _controller.Update(1, new UpdateActivityRequest
        {
            Type = "Note",
            Subject = "S",
            OccurredAt = null
        });

        activity.OccurredAt.Should().Be(original);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenActivityDoesNotExist()
    {
        _activities.Setup(a => a.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _activities.Verify(a => a.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _activities.Setup(a => a.ExistsAsync(7)).ReturnsAsync(true);
        _activities.Setup(a => a.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _activities.Verify(a => a.DeleteAsync(7), Times.Once);
    }
}
