using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Dashboard;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class DashboardControllerTests
{
    private readonly Mock<IContactRepository> _contacts = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IDealRepository> _deals = new();
    private readonly Mock<IActivityRepository> _activities = new();
    private readonly DashboardController _controller;

    public DashboardControllerTests()
    {
        _controller = new DashboardController(_contacts.Object, _companies.Object, _deals.Object, _activities.Object);
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

    private void SetupDefaults(int userId)
    {
        _contacts.Setup(c => c.GetCountAsync(userId)).ReturnsAsync(0);
        _deals.Setup(d => d.GetStageStatsAsync(userId))
            .ReturnsAsync(new Dictionary<DealStage, (int Count, decimal Total)>());
        _deals.Setup(d => d.GetWonRevenueAsync(userId)).ReturnsAsync(0m);
        _activities.Setup(a => a.GetRecentAsync(userId, 10)).ReturnsAsync(new List<Activity>());
        _companies.Setup(c => c.GetAllAsync(userId, null, It.IsAny<PaginationParams>()))
            .ReturnsAsync((new List<Company>(), 0));
        _deals.Setup(d => d.GetAllAsync(userId, It.IsAny<DealFilterParams>()))
            .ReturnsAsync((new List<Deal>(), 0));
    }

    // ---- Get ----

    [Fact]
    public async Task Get_ReturnsDashboardDtoWithAllAggregates()
    {
        _contacts.Setup(c => c.GetCountAsync(1)).ReturnsAsync(42);
        _companies.Setup(c => c.GetAllAsync(1, null, It.IsAny<PaginationParams>()))
            .ReturnsAsync((new List<Company>(), 15));
        _deals.Setup(d => d.GetAllAsync(1, It.IsAny<DealFilterParams>()))
            .ReturnsAsync((new List<Deal>(), 23));
        _deals.Setup(d => d.GetStageStatsAsync(1)).ReturnsAsync(new Dictionary<DealStage, (int Count, decimal Total)>
        {
            [DealStage.Lead] = (5, 5000m),
            [DealStage.Qualified] = (3, 3000m),
            [DealStage.Proposal] = (2, 2000m),
            [DealStage.Negotiation] = (1, 1000m),
            [DealStage.Won] = (4, 40000m),
            [DealStage.Lost] = (2, 2000m)
        });
        _deals.Setup(d => d.GetWonRevenueAsync(1)).ReturnsAsync(40000m);
        _activities.Setup(a => a.GetRecentAsync(1, 10)).ReturnsAsync(new List<Activity>());

        var result = await _controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DashboardDto>().Subject;
        dto.TotalContacts.Should().Be(42);
        dto.TotalCompanies.Should().Be(15);
        dto.TotalDeals.Should().Be(23);
        dto.WonRevenue.Should().Be(40000m);
        dto.TotalPipelineValue.Should().Be(11000m); // Lead + Qualified + Proposal + Negotiation
        dto.DealsByStage.Should().HaveCount(6);
    }

    [Fact]
    public async Task Get_TotalPipelineValueExcludesWonAndLost()
    {
        SetupDefaults(1);
        _deals.Setup(d => d.GetStageStatsAsync(1)).ReturnsAsync(new Dictionary<DealStage, (int Count, decimal Total)>
        {
            [DealStage.Lead] = (1, 100m),
            [DealStage.Won] = (1, 99999m),
            [DealStage.Lost] = (1, 8888m)
        });

        var result = await _controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DashboardDto>().Subject;
        dto.TotalPipelineValue.Should().Be(100m);
    }

    [Fact]
    public async Task Get_DealsByStageMapsAllStages()
    {
        SetupDefaults(1);
        _deals.Setup(d => d.GetStageStatsAsync(1)).ReturnsAsync(new Dictionary<DealStage, (int Count, decimal Total)>
        {
            [DealStage.Lead] = (5, 5000m),
            [DealStage.Won] = (10, 100000m)
        });

        var result = await _controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DashboardDto>().Subject;
        dto.DealsByStage.Should().HaveCount(2);
        var lead = dto.DealsByStage.First(s => s.Stage == "Lead");
        lead.Count.Should().Be(5);
        lead.TotalValue.Should().Be(5000m);
        var won = dto.DealsByStage.First(s => s.Stage == "Won");
        won.Count.Should().Be(10);
        won.TotalValue.Should().Be(100000m);
    }

    [Fact]
    public async Task Get_RecentActivitiesMapsToDto()
    {
        SetupDefaults(1);
        var contact = new Contact { Id = 5, FirstName = "Jane", LastName = "Doe" };
        var user = new User { Id = 1, Name = "Bob", Email = "b@b.com" };
        _activities.Setup(a => a.GetRecentAsync(1, 10)).ReturnsAsync(new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Discovery", Contact = contact, ContactId = 5, User = user, OccurredAt = new DateTime(2024, 6, 1) }
        });

        var result = await _controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DashboardDto>().Subject;
        dto.RecentActivities.Should().HaveCount(1);
        dto.RecentActivities[0].Type.Should().Be("Call");
        dto.RecentActivities[0].Subject.Should().Be("Discovery");
        dto.RecentActivities[0].ContactName.Should().Be("Jane Doe");
        dto.RecentActivities[0].UserName.Should().Be("Bob");
    }

    [Fact]
    public async Task Get_RecentActivities_HandlesNullContactAndUser()
    {
        SetupDefaults(1);
        _activities.Setup(a => a.GetRecentAsync(1, 10)).ReturnsAsync(new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Note, Subject = "Internal", Contact = null, User = null! }
        });

        var result = await _controller.Get();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DashboardDto>().Subject;
        dto.RecentActivities[0].ContactName.Should().BeNull();
        dto.RecentActivities[0].UserName.Should().Be("");
    }

    [Fact]
    public async Task Get_UsesAuthenticatedUserId()
    {
        SetAuthenticatedUser(99);
        SetupDefaults(99);

        await _controller.Get();

        _contacts.Verify(c => c.GetCountAsync(99), Times.Once);
        _deals.Verify(d => d.GetStageStatsAsync(99), Times.Once);
        _deals.Verify(d => d.GetWonRevenueAsync(99), Times.Once);
        _activities.Verify(a => a.GetRecentAsync(99, 10), Times.Once);
    }

    [Fact]
    public async Task Get_RequestsRecent10Activities()
    {
        SetupDefaults(1);

        await _controller.Get();

        _activities.Verify(a => a.GetRecentAsync(1, 10), Times.Once);
    }

    [Fact]
    public async Task Get_UsesPerPage1ForCompanyAndDealCounts()
    {
        SetupDefaults(1);

        await _controller.Get();

        _companies.Verify(c => c.GetAllAsync(1, null, It.Is<PaginationParams>(p => p.PerPage == 1)), Times.Once);
        _deals.Verify(d => d.GetAllAsync(1, It.Is<DealFilterParams>(f => f.PerPage == 1)), Times.Once);
    }
}
