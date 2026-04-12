using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Tests.Unit.Controllers;

public class AiControllerTests
{
    private readonly Mock<IAiService> _ai = new();
    private readonly Mock<IDealRepository> _deals = new();
    private readonly Mock<IActivityRepository> _activities = new();
    private readonly Mock<IContactRepository> _contacts = new();
    private readonly Mock<IAiInsightRepository> _insights = new();
    private readonly AiController _controller;

    public AiControllerTests()
    {
        _controller = new AiController(_ai.Object, _deals.Object, _activities.Object, _contacts.Object, _insights.Object);
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

    // ---- Summarize ----

    [Fact]
    public async Task Summarize_ReturnsOkWithSummary()
    {
        _ai.Setup(a => a.SummarizeAsync("Long text")).ReturnsAsync("Short summary");

        var result = await _controller.Summarize(new SummarizeRequest { Text = "Long text" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SummarizeResponse>().Subject;
        response.Summary.Should().Be("Short summary");
    }

    [Fact]
    public async Task Summarize_PassesTextToService()
    {
        _ai.Setup(a => a.SummarizeAsync(It.IsAny<string>())).ReturnsAsync("");

        await _controller.Summarize(new SummarizeRequest { Text = "input text" });

        _ai.Verify(a => a.SummarizeAsync("input text"), Times.Once);
    }

    // ---- DealInsights ----

    [Fact]
    public async Task DealInsights_ReturnsNotFound_WhenDealDoesNotExist()
    {
        _deals.Setup(d => d.GetByIdAsync(99)).ReturnsAsync((Deal?)null);

        var result = await _controller.DealInsights(99);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { message = "Deal not found." });
        _ai.Verify(a => a.GenerateDealInsightsAsync(It.IsAny<Deal>(), It.IsAny<List<Activity>>()), Times.Never);
        _insights.Verify(i => i.CreateAsync(It.IsAny<AiInsight>()), Times.Never);
    }

    [Fact]
    public async Task DealInsights_GeneratesAndPersistsInsight()
    {
        var deal = new Deal { Id = 5, Title = "Big Deal", Value = 100000m, Stage = DealStage.Negotiation };
        var history = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "Discovery" }
        };
        _deals.Setup(d => d.GetByIdAsync(5)).ReturnsAsync(deal);
        _activities.Setup(a => a.GetForDealAsync(5)).ReturnsAsync(history);
        _ai.Setup(a => a.GenerateDealInsightsAsync(deal, history)).ReturnsAsync("Insight content");
        _insights.Setup(i => i.CreateAsync(It.IsAny<AiInsight>()))
            .Callback<AiInsight>(ai => ai.Id = 100)
            .ReturnsAsync((AiInsight ai) => ai);

        var result = await _controller.DealInsights(5);

        _insights.Verify(i => i.CreateAsync(It.Is<AiInsight>(ai =>
            ai.DealId == 5 && ai.Insight == "Insight content")), Times.Once);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<DealInsightDto>().Subject;
        dto.Id.Should().Be(100);
        dto.Insight.Should().Be("Insight content");
        dto.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DealInsights_FetchesActivitiesForDeal()
    {
        var deal = new Deal { Id = 5, Title = "Deal" };
        _deals.Setup(d => d.GetByIdAsync(5)).ReturnsAsync(deal);
        _activities.Setup(a => a.GetForDealAsync(5)).ReturnsAsync(new List<Activity>());
        _ai.Setup(a => a.GenerateDealInsightsAsync(It.IsAny<Deal>(), It.IsAny<List<Activity>>())).ReturnsAsync("");
        _insights.Setup(i => i.CreateAsync(It.IsAny<AiInsight>())).ReturnsAsync((AiInsight ai) => ai);

        await _controller.DealInsights(5);

        _activities.Verify(a => a.GetForDealAsync(5), Times.Once);
    }

    // ---- SmartSearch ----

    [Fact]
    public async Task SmartSearch_ReturnsServiceResult()
    {
        SetAuthenticatedUser(7);
        var contacts = new List<Contact> { new() { Id = 1, FirstName = "Jane", LastName = "Doe" } };
        var activities = new List<Activity> { new() { Id = 1, Subject = "Call" } };
        var expectedResponse = new SmartSearchResponse
        {
            Interpretation = "Searching for Jane",
            Contacts = new List<ContactDto> { new() { Id = 1, FirstName = "Jane", LastName = "Doe" } },
            Activities = new List<ActivityDto>()
        };

        _contacts.Setup(c => c.GetAllAsync(7, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((contacts, 1));
        _activities.Setup(a => a.GetAllAsync(7, It.IsAny<ActivityFilterParams>()))
            .ReturnsAsync((activities, 1));
        _ai.Setup(a => a.SmartSearchAsync("Jane", contacts, activities))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.SmartSearch(new SmartSearchRequest { Query = "Jane" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SmartSearchResponse>().Subject;
        response.Interpretation.Should().Be("Searching for Jane");
        response.Contacts.Should().HaveCount(1);
    }

    [Fact]
    public async Task SmartSearch_RequestsLargePerPageFromRepositories()
    {
        _contacts.Setup(c => c.GetAllAsync(1, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((new List<Contact>(), 0));
        _activities.Setup(a => a.GetAllAsync(1, It.IsAny<ActivityFilterParams>()))
            .ReturnsAsync((new List<Activity>(), 0));
        _ai.Setup(a => a.SmartSearchAsync(It.IsAny<string>(), It.IsAny<List<Contact>>(), It.IsAny<List<Activity>>()))
            .ReturnsAsync(new SmartSearchResponse());

        await _controller.SmartSearch(new SmartSearchRequest { Query = "x" });

        _contacts.Verify(c => c.GetAllAsync(1, It.Is<ContactFilterParams>(f => f.PerPage == 100)), Times.Once);
        _activities.Verify(a => a.GetAllAsync(1, It.Is<ActivityFilterParams>(f => f.PerPage == 100)), Times.Once);
    }

    [Fact]
    public async Task SmartSearch_PassesQueryToService()
    {
        _contacts.Setup(c => c.GetAllAsync(1, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((new List<Contact>(), 0));
        _activities.Setup(a => a.GetAllAsync(1, It.IsAny<ActivityFilterParams>()))
            .ReturnsAsync((new List<Activity>(), 0));
        _ai.Setup(a => a.SmartSearchAsync(It.IsAny<string>(), It.IsAny<List<Contact>>(), It.IsAny<List<Activity>>()))
            .ReturnsAsync(new SmartSearchResponse());

        await _controller.SmartSearch(new SmartSearchRequest { Query = "find this" });

        _ai.Verify(a => a.SmartSearchAsync("find this", It.IsAny<List<Contact>>(), It.IsAny<List<Activity>>()), Times.Once);
    }
}
