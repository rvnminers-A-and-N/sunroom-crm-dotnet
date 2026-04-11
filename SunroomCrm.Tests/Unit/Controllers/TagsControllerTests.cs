using Microsoft.AspNetCore.Mvc;
using Moq;
using SunroomCrm.Api.Controllers;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Tests.Unit.Controllers;

public class TagsControllerTests
{
    private readonly Mock<ITagRepository> _tags = new();
    private readonly TagsController _controller;

    public TagsControllerTests()
    {
        _controller = new TagsController(_tags.Object);
    }

    private static Tag MakeTag(int id = 1, string name = "VIP", string color = "#02795F")
        => new()
        {
            Id = id,
            Name = name,
            Color = color,
            CreatedAt = new DateTime(2024, 1, 1)
        };

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsOkWithMappedTagDtos()
    {
        var tags = new List<Tag>
        {
            MakeTag(1, "VIP", "#ff0"),
            MakeTag(2, "Lead", "#0f0")
        };
        _tags.Setup(t => t.GetAllAsync()).ReturnsAsync(tags);

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<TagDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].Name.Should().Be("VIP");
        dtos[0].Color.Should().Be("#ff0");
        dtos[1].Name.Should().Be("Lead");
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoTags()
    {
        _tags.Setup(t => t.GetAllAsync()).ReturnsAsync(new List<Tag>());

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<TagDto>>().Subject.Should().BeEmpty();
    }

    // ---- Create ----

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameAlreadyExists()
    {
        _tags.Setup(t => t.NameExistsAsync("VIP", It.IsAny<int?>())).ReturnsAsync(true);

        var result = await _controller.Create(new CreateTagRequest { Name = "VIP", Color = "#000" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { message = "Tag name already exists." });
        _tags.Verify(t => t.CreateAsync(It.IsAny<Tag>()), Times.Never);
    }

    [Fact]
    public async Task Create_PersistsTagAndReturnsCreatedAtAction()
    {
        _tags.Setup(t => t.NameExistsAsync("New", It.IsAny<int?>())).ReturnsAsync(false);
        _tags.Setup(t => t.CreateAsync(It.IsAny<Tag>()))
            .Callback<Tag>(t => t.Id = 100)
            .ReturnsAsync((Tag t) => t);

        var result = await _controller.Create(new CreateTagRequest { Name = "New", Color = "#abcdef" });

        _tags.Verify(t => t.CreateAsync(It.Is<Tag>(tag =>
            tag.Name == "New" && tag.Color == "#abcdef")), Times.Once);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(TagsController.GetAll));
        var dto = created.Value.Should().BeOfType<TagDto>().Subject;
        dto.Id.Should().Be(100);
        dto.Name.Should().Be("New");
        dto.Color.Should().Be("#abcdef");
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTagDoesNotExist()
    {
        _tags.Setup(t => t.GetByIdAsync(99)).ReturnsAsync((Tag?)null);

        var result = await _controller.Update(99, new UpdateTagRequest { Name = "X", Color = "#000" });

        result.Should().BeOfType<NotFoundResult>();
        _tags.Verify(t => t.UpdateAsync(It.IsAny<Tag>()), Times.Never);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameConflictsWithDifferentTag()
    {
        var tag = MakeTag(1, "Old");
        _tags.Setup(t => t.GetByIdAsync(1)).ReturnsAsync(tag);
        _tags.Setup(t => t.NameExistsAsync("Conflict", (int?)1)).ReturnsAsync(true);

        var result = await _controller.Update(1, new UpdateTagRequest { Name = "Conflict", Color = "#000" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { message = "Tag name already exists." });
        _tags.Verify(t => t.UpdateAsync(It.IsAny<Tag>()), Times.Never);
    }

    [Fact]
    public async Task Update_ExcludesSelfFromNameConflictCheck()
    {
        var tag = MakeTag(5, "Same");
        _tags.Setup(t => t.GetByIdAsync(5)).ReturnsAsync(tag);
        _tags.Setup(t => t.NameExistsAsync("Same", (int?)5)).ReturnsAsync(false);
        _tags.Setup(t => t.UpdateAsync(tag)).Returns(Task.CompletedTask);

        var result = await _controller.Update(5, new UpdateTagRequest { Name = "Same", Color = "#fff" });

        result.Should().BeOfType<OkObjectResult>();
        _tags.Verify(t => t.NameExistsAsync("Same", (int?)5), Times.Once);
    }

    [Fact]
    public async Task Update_AppliesNameAndColor_AndReturnsOk()
    {
        var tag = MakeTag(1, "Old", "#000");
        _tags.Setup(t => t.GetByIdAsync(1)).ReturnsAsync(tag);
        _tags.Setup(t => t.NameExistsAsync("New", (int?)1)).ReturnsAsync(false);

        var result = await _controller.Update(1, new UpdateTagRequest { Name = "New", Color = "#fff" });

        tag.Name.Should().Be("New");
        tag.Color.Should().Be("#fff");
        _tags.Verify(t => t.UpdateAsync(tag), Times.Once);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<TagDto>().Subject;
        dto.Name.Should().Be("New");
        dto.Color.Should().Be("#fff");
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTagDoesNotExist()
    {
        _tags.Setup(t => t.ExistsAsync(99)).ReturnsAsync(false);

        var result = await _controller.Delete(99);

        result.Should().BeOfType<NotFoundResult>();
        _tags.Verify(t => t.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _tags.Setup(t => t.ExistsAsync(7)).ReturnsAsync(true);
        _tags.Setup(t => t.DeleteAsync(7)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(7);

        result.Should().BeOfType<NoContentResult>();
        _tags.Verify(t => t.DeleteAsync(7), Times.Once);
    }
}
