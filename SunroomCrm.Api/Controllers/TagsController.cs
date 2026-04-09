using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ITagRepository _tags;

    public TagsController(ITagRepository tags)
    {
        _tags = tags;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tags = await _tags.GetAllAsync();
        return Ok(tags.Select(MapTagDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        if (await _tags.NameExistsAsync(request.Name))
            return BadRequest(new { message = "Tag name already exists." });

        var tag = new Tag
        {
            Name = request.Name,
            Color = request.Color
        };

        await _tags.CreateAsync(tag);
        return CreatedAtAction(nameof(GetAll), MapTagDto(tag));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTagRequest request)
    {
        var tag = await _tags.GetByIdAsync(id);
        if (tag == null) return NotFound();

        if (await _tags.NameExistsAsync(request.Name, id))
            return BadRequest(new { message = "Tag name already exists." });

        tag.Name = request.Name;
        tag.Color = request.Color;

        await _tags.UpdateAsync(tag);
        return Ok(MapTagDto(tag));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _tags.ExistsAsync(id))
            return NotFound();

        await _tags.DeleteAsync(id);
        return NoContent();
    }

    private static TagDto MapTagDto(Tag t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Color = t.Color,
        CreatedAt = t.CreatedAt
    };
}
