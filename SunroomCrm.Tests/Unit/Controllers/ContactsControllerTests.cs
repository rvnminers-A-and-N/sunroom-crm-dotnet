using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class ContactsControllerTests
{
    private readonly Mock<IContactRepository> _contacts = new();
    private readonly Mock<ITagRepository> _tags = new();
    private readonly ContactsController _controller;

    public ContactsControllerTests()
    {
        _controller = new ContactsController(_contacts.Object, _tags.Object);
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

    private static Contact MakeContact(int id = 1, string first = "Jane", string last = "Doe")
        => new()
        {
            Id = id,
            UserId = 1,
            FirstName = first,
            LastName = last,
            Email = $"{first}@example.com".ToLower(),
            Phone = "555-1212",
            Title = "Engineer",
            Tags = new List<Tag>(),
            Deals = new List<Deal>(),
            Activities = new List<Activity>(),
            CreatedAt = new DateTime(2024, 1, 1)
        };

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsPaginatedResponse()
    {
        var contacts = new List<Contact>
        {
            MakeContact(1, "Jane", "Doe"),
            MakeContact(2, "John", "Smith")
        };
        contacts[0].Company = new Company { Id = 5, Name = "Acme" };
        contacts[0].CompanyId = 5;
        contacts[0].Tags = new List<Tag>
        {
            new() { Id = 1, Name = "VIP", Color = "#ff0", CreatedAt = DateTime.UtcNow }
        };

        _contacts.Setup(c => c.GetAllAsync(1, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((contacts, 2));

        var filter = new ContactFilterParams { Page = 1, PerPage = 25 };
        var result = await _controller.GetAll(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<ContactDto>>().Subject;
        paged.Data.Should().HaveCount(2);
        paged.Data[0].FirstName.Should().Be("Jane");
        paged.Data[0].CompanyName.Should().Be("Acme");
        paged.Data[0].CompanyId.Should().Be(5);
        paged.Data[0].Tags.Should().HaveCount(1);
        paged.Data[0].Tags[0].Name.Should().Be("VIP");
        paged.Meta.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_PassesFilterToRepository()
    {
        var filter = new ContactFilterParams { Page = 2, PerPage = 10, Search = "jane", CompanyId = 5, TagId = 3 };
        _contacts.Setup(c => c.GetAllAsync(1, filter)).ReturnsAsync((new List<Contact>(), 0));

        await _controller.GetAll(filter);

        _contacts.Verify(c => c.GetAllAsync(1, filter), Times.Once);
    }

    [Fact]
    public async Task GetAll_CalculatesLastPage()
    {
        _contacts.Setup(c => c.GetAllAsync(1, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((new List<Contact>(), 23));

        var result = await _controller.GetAll(new ContactFilterParams { Page = 1, PerPage = 10 });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<ContactDto>>().Subject;
        paged.Meta.LastPage.Should().Be(3);
    }

    [Fact]
    public async Task GetAll_HandlesNullCompany()
    {
        var contact = MakeContact(1);
        contact.Company = null;
        contact.CompanyId = null;
        _contacts.Setup(c => c.GetAllAsync(1, It.IsAny<ContactFilterParams>()))
            .ReturnsAsync((new List<Contact> { contact }, 1));

        var result = await _controller.GetAll(new ContactFilterParams());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeOfType<PaginatedResponse<ContactDto>>().Subject;
        paged.Data[0].CompanyName.Should().BeNull();
        paged.Data[0].CompanyId.Should().BeNull();
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenContactDoesNotExist()
    {
        _contacts.Setup(c => c.GetByIdWithDetailsAsync(99)).ReturnsAsync((Contact?)null);

        var result = await _controller.GetById(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsContactDetailDto_WhenFound()
    {
        var contact = MakeContact(5, "Alice", "Wonder");
        contact.Company = new Company { Id = 10, Name = "Wonderland", Industry = "Magic", City = "Atlantis", State = "OC" };
        contact.CompanyId = 10;
        contact.Tags = new List<Tag>
        {
            new() { Id = 1, Name = "VIP", Color = "#000", CreatedAt = DateTime.UtcNow }
        };
        contact.Deals = new List<Deal>
        {
            new() { Id = 99, Title = "Deal A", Value = 1000m, Stage = DealStage.Won, ContactId = 5, CompanyId = 10, Company = contact.Company, UserId = 1 }
        };
        var user = new User { Id = 1, Name = "Bob", Email = "b@b.com" };
        contact.Activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Call, Subject = "First", Body = "B", ContactId = 5, OccurredAt = new DateTime(2024, 1, 1), CreatedAt = new DateTime(2024, 1, 1), User = user, UserId = 1 },
            new() { Id = 2, Type = ActivityType.Note, Subject = "Second", Body = "B2", ContactId = 5, OccurredAt = new DateTime(2024, 2, 1), CreatedAt = new DateTime(2024, 2, 1), User = user, UserId = 1 }
        };

        _contacts.Setup(c => c.GetByIdWithDetailsAsync(5)).ReturnsAsync(contact);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<ContactDetailDto>().Subject;
        detail.Id.Should().Be(5);
        detail.FirstName.Should().Be("Alice");
        detail.LastName.Should().Be("Wonder");
        detail.Company.Should().NotBeNull();
        detail.Company!.Name.Should().Be("Wonderland");
        detail.Tags.Should().HaveCount(1);
        detail.Tags[0].Name.Should().Be("VIP");
        detail.Deals.Should().HaveCount(1);
        detail.Deals[0].Stage.Should().Be("Won");
        detail.Deals[0].ContactName.Should().Be("Alice Wonder");
        detail.Deals[0].CompanyName.Should().Be("Wonderland");
        detail.Activities.Should().HaveCount(2);
        detail.Activities[0].Subject.Should().Be("Second"); // ordered by OccurredAt desc
        detail.Activities[0].UserName.Should().Be("Bob");
    }

    [Fact]
    public async Task GetById_HandlesNullCompany()
    {
        var contact = MakeContact(5);
        contact.Company = null;
        _contacts.Setup(c => c.GetByIdWithDetailsAsync(5)).ReturnsAsync(contact);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<ContactDetailDto>().Subject;
        detail.Company.Should().BeNull();
    }

    [Fact]
    public async Task GetById_HandlesNullActivityUser()
    {
        var contact = MakeContact(5);
        contact.Activities = new List<Activity>
        {
            new() { Id = 1, Type = ActivityType.Email, Subject = "S", Body = "B", ContactId = 5, OccurredAt = DateTime.UtcNow, User = null!, UserId = 1 }
        };
        _contacts.Setup(c => c.GetByIdWithDetailsAsync(5)).ReturnsAsync(contact);

        var result = await _controller.GetById(5);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<ContactDetailDto>().Subject;
        detail.Activities[0].UserName.Should().Be("");
    }

    // ---- Create ----

    [Fact]
    public async Task Create_PersistsContactAndReturnsCreatedAtAction()
    {
        SetAuthenticatedUser(7);
        Contact? createdContact = null;
        _contacts.Setup(c => c.CreateAsync(It.IsAny<Contact>()))
            .Callback<Contact>(c => { c.Id = 100; createdContact = c; })
            .ReturnsAsync((Contact c) => c);
        _contacts.Setup(c => c.GetByIdAsync(100)).ReturnsAsync(() => createdContact);

        var result = await _controller.Create(new CreateContactRequest
        {
            FirstName = "New",
            LastName = "Contact",
            Email = "new@example.com",
            Phone = "555",
            Title = "Dev",
            Notes = "Notes",
            CompanyId = 3
        });

        _contacts.Verify(c => c.CreateAsync(It.Is<Contact>(co =>
            co.UserId == 7 &&
            co.FirstName == "New" &&
            co.LastName == "Contact" &&
            co.Email == "new@example.com" &&
            co.CompanyId == 3)), Times.Once);
        _contacts.Verify(c => c.SyncTagsAsync(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues!["id"].Should().Be(100);
        created.Value.Should().BeOfType<ContactDto>();
    }

    [Fact]
    public async Task Create_SyncsTags_WhenTagIdsProvided()
    {
        _contacts.Setup(c => c.CreateAsync(It.IsAny<Contact>()))
            .Callback<Contact>(c => c.Id = 50)
            .ReturnsAsync((Contact c) => c);
        _contacts.Setup(c => c.GetByIdAsync(50)).ReturnsAsync(MakeContact(50));

        await _controller.Create(new CreateContactRequest
        {
            FirstName = "T",
            LastName = "G",
            TagIds = new List<int> { 1, 2, 3 }
        });

        _contacts.Verify(c => c.SyncTagsAsync(50, It.Is<List<int>>(ids =>
            ids.Count == 3 && ids.Contains(1) && ids.Contains(2) && ids.Contains(3))), Times.Once);
    }

    [Fact]
    public async Task Create_SkipsSyncTags_WhenTagIdsIsEmpty()
    {
        _contacts.Setup(c => c.CreateAsync(It.IsAny<Contact>()))
            .Callback<Contact>(c => c.Id = 50)
            .ReturnsAsync((Contact c) => c);
        _contacts.Setup(c => c.GetByIdAsync(50)).ReturnsAsync(MakeContact(50));

        await _controller.Create(new CreateContactRequest
        {
            FirstName = "T",
            LastName = "G",
            TagIds = new List<int>()
        });

        _contacts.Verify(c => c.SyncTagsAsync(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenContactDoesNotExist()
    {
        _contacts.Setup(c => c.GetByIdAsync(99)).ReturnsAsync((Contact?)null);

        var result = await _controller.Update(99, new UpdateContactRequest { FirstName = "X", LastName = "Y" });

        result.Should().BeOfType<NotFoundResult>();
        _contacts.Verify(c => c.UpdateAsync(It.IsAny<Contact>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesAllFields_AndReturnsOkDto()
    {
        var contact = MakeContact(1, "Old", "Name");
        _contacts.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(contact);

        var result = await _controller.Update(1, new UpdateContactRequest
        {
            FirstName = "New",
            LastName = "Person",
            Email = "new@x.com",
            Phone = "999",
            Title = "Lead",
            Notes = "Notes",
            CompanyId = 7
        });

        contact.FirstName.Should().Be("New");
        contact.LastName.Should().Be("Person");
        contact.Email.Should().Be("new@x.com");
        contact.Phone.Should().Be("999");
        contact.Title.Should().Be("Lead");
        contact.Notes.Should().Be("Notes");
        contact.CompanyId.Should().Be(7);
        _contacts.Verify(c => c.UpdateAsync(contact), Times.Once);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ContactDto>();
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenContactDoesNotExist()
    {
        _contacts.Setup(c => c.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _contacts.Verify(c => c.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _contacts.Setup(c => c.ExistsAsync(7)).ReturnsAsync(true);
        _contacts.Setup(c => c.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _contacts.Verify(c => c.DeleteAsync(7), Times.Once);
    }

    // ---- SyncTags ----

    [Fact]
    public async Task SyncTags_ReturnsNotFound_WhenContactDoesNotExist()
    {
        _contacts.Setup(c => c.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.SyncTags(99, new SyncTagsRequest { TagIds = new List<int> { 1 } });

        result.Should().BeOfType<NotFoundResult>();
        _contacts.Verify(c => c.SyncTagsAsync(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Fact]
    public async Task SyncTags_PersistsTagIdsAndReturnsUpdatedContact()
    {
        var contact = MakeContact(5);
        contact.Tags = new List<Tag>
        {
            new() { Id = 1, Name = "VIP", Color = "#000", CreatedAt = DateTime.UtcNow }
        };
        _contacts.Setup(c => c.ExistsAsync(5)).ReturnsAsync(true);
        _contacts.Setup(c => c.SyncTagsAsync(5, It.IsAny<List<int>>())).Returns(Task.CompletedTask);
        _contacts.Setup(c => c.GetByIdAsync(5)).ReturnsAsync(contact);

        var result = await _controller.SyncTags(5, new SyncTagsRequest { TagIds = new List<int> { 1, 2 } });

        _contacts.Verify(c => c.SyncTagsAsync(5, It.Is<List<int>>(ids =>
            ids.Count == 2 && ids.Contains(1) && ids.Contains(2))), Times.Once);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ContactDto>().Subject;
        dto.Id.Should().Be(5);
        dto.Tags.Should().HaveCount(1);
    }
}
