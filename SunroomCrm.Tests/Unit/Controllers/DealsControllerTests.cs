using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class DealsControllerTests
{
    private readonly Mock<IDealRepository> _deals = new();
    private readonly Mock<IContactRepository> _contacts = new();
    private readonly DealsController _controller;

    public DealsControllerTests()
    {
        _controller = new DealsController(_deals.Object, _contacts.Object);
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

    private static Deal MakeDeal(int id = 1, DealStage stage = DealStage.Lead)
    {
        var contact = new Contact { Id = 10, FirstName = "Jane", LastName = "Doe", Email = "j@d.com" };
        return new Deal
        {
            Id = id,
            UserId = 1,
            ContactId = 10,
            Contact = contact,
            CompanyId = 5,
            Company = new Company { Id = 5, Name = "Acme" },
            Title = "Test Deal",
            Value = 5000m,
            Stage = stage,
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 1),
            Activities = new List<Activity>(),
            AiInsights = new List<AiInsight>()
        };
    }

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsPaginatedResponse()
    {
        var deals = new List<Deal> { MakeDeal(1), MakeDeal(2, DealStage.Won) };
        _deals.Setup(d => d.GetAllAsync(1, It.IsAny<DealFilterParams>())).ReturnsAsync((deals, 2));

        var result = await _controller.GetAll(new DealFilterParams { Page = 1, PerPage = 25 });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<DealDto>>().Subject;
        paged.Data.Should().HaveCount(2);
        paged.Data[0].Title.Should().Be("Test Deal");
        paged.Data[0].ContactName.Should().Be("Jane Doe");
        paged.Data[0].CompanyName.Should().Be("Acme");
        paged.Data[1].Stage.Should().Be("Won");
        paged.Meta.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_PassesFilterAndUserId()
    {
        var filter = new DealFilterParams { Page = 1, PerPage = 25, Stage = "Won", Search = "x" };
        _deals.Setup(d => d.GetAllAsync(1, filter)).ReturnsAsync((new List<Deal>(), 0));

        await _controller.GetAll(filter);

        _deals.Verify(d => d.GetAllAsync(1, filter), Times.Once);
    }

    [Fact]
    public async Task GetAll_HandlesNullContactInDto()
    {
        var deal = MakeDeal(1);
        deal.Contact = null!;
        _deals.Setup(d => d.GetAllAsync(1, It.IsAny<DealFilterParams>()))
            .ReturnsAsync((new List<Deal> { deal }, 1));

        var result = await _controller.GetAll(new DealFilterParams());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<DealDto>>().Subject;
        paged.Data[0].ContactName.Should().Be("");
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenDealDoesNotExist()
    {
        _deals.Setup(d => d.GetByIdWithDetailsAsync(99)).ReturnsAsync((Deal?)null);

        var result = await _controller.GetById(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsDealDetailDto_WhenFound()
    {
        var deal = MakeDeal(5, DealStage.Proposal);
        deal.Notes = "Important deal";
        var user = new User { Id = 1, Name = "Bob", Email = "b@b.com" };
        deal.Activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "First", ContactId = 10, DealId = 5, OccurredAt = new DateTime(2024, 1, 1), CreatedAt = new DateTime(2024, 1, 1), User = user, UserId = 1 },
            new() { Id = 2, Type = ActivityType.Note, Subject = "Second", ContactId = 10, DealId = 5, OccurredAt = new DateTime(2024, 2, 1), CreatedAt = new DateTime(2024, 2, 1), User = user, UserId = 1 }
        };
        deal.AiInsights = new List<AiInsight>
        {
            new() { Id = 1, DealId = 5, Insight = "Old insight", GeneratedAt = new DateTime(2024, 1, 1) },
            new() { Id = 2, DealId = 5, Insight = "Newer insight", GeneratedAt = new DateTime(2024, 3, 1) }
        };
        _deals.Setup(d => d.GetByIdWithDetailsAsync(5)).ReturnsAsync(deal);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<DealDetailDto>().Subject;
        detail.Id.Should().Be(5);
        detail.Stage.Should().Be("Proposal");
        detail.ContactName.Should().Be("Jane Doe");
        detail.CompanyName.Should().Be("Acme");
        detail.Notes.Should().Be("Important deal");
        detail.Activities.Should().HaveCount(2);
        detail.Activities[0].Subject.Should().Be("Second"); // ordered desc
        detail.Activities[0].DealTitle.Should().Be("Test Deal");
        detail.Insights.Should().HaveCount(2);
        detail.Insights[0].Insight.Should().Be("Newer insight"); // ordered desc
    }

    [Fact]
    public async Task GetById_HandlesNullCompany()
    {
        var deal = MakeDeal(5);
        deal.Company = null;
        deal.CompanyId = null;
        _deals.Setup(d => d.GetByIdWithDetailsAsync(5)).ReturnsAsync(deal);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<DealDetailDto>().Subject;
        detail.CompanyName.Should().BeNull();
        detail.CompanyId.Should().BeNull();
    }

    // ---- Create ----

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenContactDoesNotExist()
    {
        _contacts.Setup(c => c.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Create(new CreateDealRequest
        {
            Title = "T",
            Value = 100m,
            ContactId = 99,
            Stage = "Lead"
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { message = "Contact not found." });
        _deals.Verify(d => d.CreateAsync(It.IsAny<Deal>()), Times.Never);
    }

    [Fact]
    public async Task Create_PersistsDealAndReturnsCreatedAtAction()
    {
        SetAuthenticatedUser(7);
        _contacts.Setup(c => c.ExistsAsync(10)).ReturnsAsync(true);
        Deal? created = null;
        _deals.Setup(d => d.CreateAsync(It.IsAny<Deal>()))
            .Callback<Deal>(d => { d.Id = 100; created = d; })
            .ReturnsAsync((Deal d) => d);
        _deals.Setup(d => d.GetByIdAsync(100)).ReturnsAsync(() => created);

        var result = await _controller.Create(new CreateDealRequest
        {
            Title = "Big Deal",
            Value = 50000m,
            ContactId = 10,
            CompanyId = 5,
            Stage = "Qualified",
            ExpectedCloseDate = new DateOnly(2024, 12, 31),
            Notes = "Important"
        });

        _deals.Verify(d => d.CreateAsync(It.Is<Deal>(deal =>
            deal.UserId == 7 &&
            deal.Title == "Big Deal" &&
            deal.Value == 50000m &&
            deal.ContactId == 10 &&
            deal.CompanyId == 5 &&
            deal.Stage == DealStage.Qualified &&
            deal.Notes == "Important")), Times.Once);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.RouteValues!["id"].Should().Be(100);
    }

    [Fact]
    public async Task Create_DefaultsStageToLead_WhenStageIsInvalid()
    {
        _contacts.Setup(c => c.ExistsAsync(10)).ReturnsAsync(true);
        _deals.Setup(d => d.CreateAsync(It.IsAny<Deal>()))
            .Callback<Deal>(d => d.Id = 1)
            .ReturnsAsync((Deal d) => d);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(MakeDeal(1));

        await _controller.Create(new CreateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "Bogus"
        });

        _deals.Verify(d => d.CreateAsync(It.Is<Deal>(deal => deal.Stage == DealStage.Lead)), Times.Once);
    }

    [Theory]
    [InlineData("won", DealStage.Won)]
    [InlineData("LEAD", DealStage.Lead)]
    [InlineData("Negotiation", DealStage.Negotiation)]
    public async Task Create_ParsesStageCaseInsensitively(string stage, DealStage expected)
    {
        _contacts.Setup(c => c.ExistsAsync(10)).ReturnsAsync(true);
        _deals.Setup(d => d.CreateAsync(It.IsAny<Deal>()))
            .Callback<Deal>(d => d.Id = 1)
            .ReturnsAsync((Deal d) => d);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(MakeDeal(1));

        await _controller.Create(new CreateDealRequest { Title = "T", Value = 1m, ContactId = 10, Stage = stage });

        _deals.Verify(d => d.CreateAsync(It.Is<Deal>(deal => deal.Stage == expected)), Times.Once);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenDealDoesNotExist()
    {
        _deals.Setup(d => d.GetByIdAsync(99)).ReturnsAsync((Deal?)null);

        var result = await _controller.Update(99, new UpdateDealRequest { Title = "T", Value = 1m, ContactId = 10 });

        result.Should().BeOfType<NotFoundResult>();
        _deals.Verify(d => d.UpdateAsync(It.IsAny<Deal>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesAllFields()
    {
        var deal = MakeDeal(1, DealStage.Lead);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "Updated",
            Value = 99999m,
            ContactId = 20,
            CompanyId = 8,
            Stage = "Qualified",
            ExpectedCloseDate = new DateOnly(2025, 6, 1),
            Notes = "Updated notes"
        });

        deal.Title.Should().Be("Updated");
        deal.Value.Should().Be(99999m);
        deal.ContactId.Should().Be(20);
        deal.CompanyId.Should().Be(8);
        deal.Stage.Should().Be(DealStage.Qualified);
        deal.ExpectedCloseDate.Should().Be(new DateOnly(2025, 6, 1));
        deal.Notes.Should().Be("Updated notes");
        _deals.Verify(d => d.UpdateAsync(deal), Times.Once);
    }

    [Fact]
    public async Task Update_SetsClosedAt_WhenStageChangesToWon()
    {
        var deal = MakeDeal(1, DealStage.Negotiation);
        deal.ClosedAt = null;
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "Won"
        });

        deal.ClosedAt.Should().NotBeNull();
        deal.Stage.Should().Be(DealStage.Won);
    }

    [Fact]
    public async Task Update_SetsClosedAt_WhenStageChangesToLost()
    {
        var deal = MakeDeal(1, DealStage.Proposal);
        deal.ClosedAt = null;
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "Lost"
        });

        deal.ClosedAt.Should().NotBeNull();
        deal.Stage.Should().Be(DealStage.Lost);
    }

    [Fact]
    public async Task Update_DoesNotResetClosedAt_WhenStillWon()
    {
        var existingClosedAt = new DateTime(2024, 1, 1);
        var deal = MakeDeal(1, DealStage.Won);
        deal.ClosedAt = existingClosedAt;
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "Won"
        });

        deal.ClosedAt.Should().Be(existingClosedAt);
    }

    [Fact]
    public async Task Update_ClearsClosedAt_WhenMovingFromWonBackToOpenStage()
    {
        var deal = MakeDeal(1, DealStage.Won);
        deal.ClosedAt = new DateTime(2024, 1, 1);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "Negotiation"
        });

        deal.ClosedAt.Should().BeNull();
        deal.Stage.Should().Be(DealStage.Negotiation);
    }

    [Fact]
    public async Task Update_LeavesStageUnchanged_WhenStageIsNullOrWhitespace()
    {
        var deal = MakeDeal(1, DealStage.Qualified);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "   "
        });

        deal.Stage.Should().Be(DealStage.Qualified);
    }

    [Fact]
    public async Task Update_LeavesStageUnchanged_WhenStageIsInvalid()
    {
        var deal = MakeDeal(1, DealStage.Qualified);
        _deals.Setup(d => d.GetByIdAsync(1)).ReturnsAsync(deal);

        await _controller.Update(1, new UpdateDealRequest
        {
            Title = "T",
            Value = 1m,
            ContactId = 10,
            Stage = "BogusStage"
        });

        deal.Stage.Should().Be(DealStage.Qualified);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenDealDoesNotExist()
    {
        _deals.Setup(d => d.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _deals.Verify(d => d.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _deals.Setup(d => d.ExistsAsync(7)).ReturnsAsync(true);
        _deals.Setup(d => d.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _deals.Verify(d => d.DeleteAsync(7), Times.Once);
    }

    // ---- Pipeline ----

    [Fact]
    public async Task Pipeline_ReturnsAllStages_WithCountAndTotal()
    {
        foreach (DealStage stage in Enum.GetValues<DealStage>())
        {
            _deals.Setup(d => d.GetByStageAsync(1, stage))
                .ReturnsAsync(stage == DealStage.Lead
                    ? new List<Deal> { MakeDeal(1), MakeDeal(2) }
                    : new List<Deal>());
        }

        var result = await _controller.Pipeline();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var pipeline = ok.Value.Should().BeOfType<PipelineDto>().Subject;
        pipeline.Stages.Should().HaveCount(6);
        pipeline.Stages[0].Stage.Should().Be("Lead");
        pipeline.Stages[0].Count.Should().Be(2);
        pipeline.Stages[0].TotalValue.Should().Be(10000m);
        pipeline.Stages[1].Stage.Should().Be("Qualified");
        pipeline.Stages[1].Count.Should().Be(0);
    }

    [Fact]
    public async Task Pipeline_UsesAuthenticatedUserId()
    {
        SetAuthenticatedUser(42);
        foreach (DealStage stage in Enum.GetValues<DealStage>())
            _deals.Setup(d => d.GetByStageAsync(42, stage)).ReturnsAsync(new List<Deal>());

        await _controller.Pipeline();

        foreach (DealStage stage in Enum.GetValues<DealStage>())
            _deals.Verify(d => d.GetByStageAsync(42, stage), Times.Once);
    }
}
