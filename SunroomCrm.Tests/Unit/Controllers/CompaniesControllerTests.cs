using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class CompaniesControllerTests
{
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly CompaniesController _controller;

    public CompaniesControllerTests()
    {
        _controller = new CompaniesController(_companies.Object);
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

    private static Company MakeCompany(int id = 1, string name = "Acme")
        => new()
        {
            Id = id,
            UserId = 1,
            Name = name,
            Industry = "Tech",
            Website = "https://acme.com",
            Phone = "555-1212",
            City = "Austin",
            State = "TX",
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 1),
            Contacts = new List<Contact>(),
            Deals = new List<Deal>()
        };

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsPaginatedResponse_WithMappedDtos()
    {
        var companies = new List<Company>
        {
            MakeCompany(1, "Acme"),
            MakeCompany(2, "Globex")
        };
        companies[0].Contacts = new List<Contact> { new() { Id = 1, FirstName = "A", LastName = "B", Email = "a@b.com" } };
        companies[0].Deals = new List<Deal> { new() { Id = 1, Title = "D", Value = 1000m, Stage = DealStage.Lead, UserId = 1 } };

        _companies.Setup(c => c.GetAllAsync(1, null, It.IsAny<PaginationParams>()))
            .ReturnsAsync((companies, 2));

        var pagination = new PaginationParams { Page = 1, PerPage = 25 };
        var result = await _controller.GetAll(null, pagination);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<CompanyDto>>().Subject;
        paged.Data.Should().HaveCount(2);
        paged.Data[0].Name.Should().Be("Acme");
        paged.Data[0].ContactCount.Should().Be(1);
        paged.Data[0].DealCount.Should().Be(1);
        paged.Meta.Total.Should().Be(2);
        paged.Meta.CurrentPage.Should().Be(1);
        paged.Meta.PerPage.Should().Be(25);
        paged.Meta.LastPage.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_ForwardsSearchAndPaginationToRepository()
    {
        _companies.Setup(c => c.GetAllAsync(1, "acme", It.IsAny<PaginationParams>()))
            .ReturnsAsync((new List<Company>(), 0));

        var pagination = new PaginationParams { Page = 2, PerPage = 10 };
        await _controller.GetAll("acme", pagination);

        _companies.Verify(c => c.GetAllAsync(1, "acme", pagination), Times.Once);
    }

    [Fact]
    public async Task GetAll_CalculatesLastPageCorrectly()
    {
        _companies.Setup(c => c.GetAllAsync(1, null, It.IsAny<PaginationParams>()))
            .ReturnsAsync((new List<Company>(), 47));

        var pagination = new PaginationParams { Page = 1, PerPage = 10 };
        var result = await _controller.GetAll(null, pagination);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<CompanyDto>>().Subject;
        paged.Meta.LastPage.Should().Be(5);
    }

    [Fact]
    public async Task GetAll_UsesAuthenticatedUserId()
    {
        SetAuthenticatedUser(99);
        _companies.Setup(c => c.GetAllAsync(99, null, It.IsAny<PaginationParams>()))
            .ReturnsAsync((new List<Company>(), 0));

        await _controller.GetAll(null, new PaginationParams());

        _companies.Verify(c => c.GetAllAsync(99, null, It.IsAny<PaginationParams>()), Times.Once);
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenCompanyDoesNotExist()
    {
        _companies.Setup(c => c.GetByIdWithDetailsAsync(99)).ReturnsAsync((Company?)null);

        var result = await _controller.GetById(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsCompanyDetailDto_WhenFound()
    {
        var company = MakeCompany(5, "Initech");
        company.Address = "123 Main";
        company.Zip = "78701";
        company.Notes = "Important client";
        var contact = new Contact { Id = 10, FirstName = "Peter", LastName = "Gibbons", Email = "p@initech.com", Phone = "555", Title = "Eng", LastContactedAt = null, CreatedAt = DateTime.UtcNow };
        company.Contacts = new List<Contact> { contact };
        company.Deals = new List<Deal>
        {
            new() { Id = 20, Title = "TPS Deal", Value = 50000m, Stage = DealStage.Proposal, ContactId = 10, Contact = contact, UserId = 1, ExpectedCloseDate = null, ClosedAt = null, CreatedAt = DateTime.UtcNow }
        };
        _companies.Setup(c => c.GetByIdWithDetailsAsync(5)).ReturnsAsync(company);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<CompanyDetailDto>().Subject;
        detail.Id.Should().Be(5);
        detail.Name.Should().Be("Initech");
        detail.Address.Should().Be("123 Main");
        detail.Zip.Should().Be("78701");
        detail.Notes.Should().Be("Important client");
        detail.Contacts.Should().HaveCount(1);
        detail.Contacts[0].FirstName.Should().Be("Peter");
        detail.Contacts[0].CompanyName.Should().Be("Initech");
        detail.Contacts[0].CompanyId.Should().Be(5);
        detail.Deals.Should().HaveCount(1);
        detail.Deals[0].Title.Should().Be("TPS Deal");
        detail.Deals[0].Stage.Should().Be("Proposal");
        detail.Deals[0].ContactName.Should().Be("Peter Gibbons");
        detail.Deals[0].CompanyName.Should().Be("Initech");
    }

    [Fact]
    public async Task GetById_HandlesNullContactInDeal()
    {
        var company = MakeCompany(5);
        company.Deals = new List<Deal>
        {
            new() { Id = 20, Title = "Deal", Value = 1m, Stage = DealStage.Lead, ContactId = 0, Contact = null!, UserId = 1 }
        };
        _companies.Setup(c => c.GetByIdWithDetailsAsync(5)).ReturnsAsync(company);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<CompanyDetailDto>().Subject;
        detail.Deals[0].ContactName.Should().Be("");
    }

    // ---- Create ----

    [Fact]
    public async Task Create_PersistsCompanyWithAuthenticatedUserId()
    {
        SetAuthenticatedUser(7);
        _companies.Setup(c => c.CreateAsync(It.IsAny<Company>()))
            .ReturnsAsync((Company c) => { c.Id = 100; return c; });

        var request = new CreateCompanyRequest
        {
            Name = "New Co",
            Industry = "Retail",
            Website = "https://new.co",
            Phone = "555-1234",
            Address = "1 St",
            City = "Austin",
            State = "TX",
            Zip = "78701",
            Notes = "Notes"
        };

        var result = await _controller.Create(request);

        _companies.Verify(c => c.CreateAsync(It.Is<Company>(co =>
            co.UserId == 7 &&
            co.Name == "New Co" &&
            co.Industry == "Retail" &&
            co.Website == "https://new.co" &&
            co.Phone == "555-1234" &&
            co.Address == "1 St" &&
            co.City == "Austin" &&
            co.State == "TX" &&
            co.Zip == "78701" &&
            co.Notes == "Notes")), Times.Once);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(CompaniesController.GetById));
        created.RouteValues!["id"].Should().Be(100);
        var dto = created.Value.Should().BeOfType<CompanyDto>().Subject;
        dto.Name.Should().Be("New Co");
        dto.ContactCount.Should().Be(0);
        dto.DealCount.Should().Be(0);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCompanyDoesNotExist()
    {
        _companies.Setup(c => c.GetByIdAsync(99)).ReturnsAsync((Company?)null);

        var result = await _controller.Update(99, new UpdateCompanyRequest { Name = "X" });

        result.Should().BeOfType<NotFoundResult>();
        _companies.Verify(c => c.UpdateAsync(It.IsAny<Company>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesAllFields_AndReturnsOkDto()
    {
        var company = MakeCompany(1, "Old");
        _companies.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(company);

        var result = await _controller.Update(1, new UpdateCompanyRequest
        {
            Name = "New",
            Industry = "Finance",
            Website = "https://new.com",
            Phone = "555",
            Address = "Addr",
            City = "Dallas",
            State = "TX",
            Zip = "75001",
            Notes = "Notes"
        });

        company.Name.Should().Be("New");
        company.Industry.Should().Be("Finance");
        company.Website.Should().Be("https://new.com");
        company.Phone.Should().Be("555");
        company.Address.Should().Be("Addr");
        company.City.Should().Be("Dallas");
        company.State.Should().Be("TX");
        company.Zip.Should().Be("75001");
        company.Notes.Should().Be("Notes");
        _companies.Verify(c => c.UpdateAsync(company), Times.Once);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<CompanyDto>().Subject;
        dto.Name.Should().Be("New");
        dto.Industry.Should().Be("Finance");
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenCompanyDoesNotExist()
    {
        _companies.Setup(c => c.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _companies.Verify(c => c.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _companies.Setup(c => c.ExistsAsync(7)).ReturnsAsync(true);
        _companies.Setup(c => c.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _companies.Verify(c => c.DeleteAsync(7), Times.Once);
    }
}
