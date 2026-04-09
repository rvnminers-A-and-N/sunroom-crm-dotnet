using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/activities")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityRepository _activities;

    public ActivitiesController(IActivityRepository activities)
    {
        _activities = activities;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ActivityFilterParams filter)
    {
        var userId = GetUserId();
        var (items, total) = await _activities.GetAllAsync(userId, filter);

        return Ok(new PaginatedResponse<ActivityDto>
        {
            Data = items.Select(MapActivityDto).ToList(),
            Meta = new PaginationMeta
            {
                CurrentPage = filter.Page,
                PerPage = filter.PerPage,
                Total = total,
                LastPage = (int)Math.Ceiling((double)total / filter.PerPage)
            }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var activity = await _activities.GetByIdAsync(id);
        if (activity == null) return NotFound();
        return Ok(MapActivityDto(activity));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequest request)
    {
        if (!Enum.TryParse<ActivityType>(request.Type, true, out var type))
            return BadRequest(new { message = "Invalid activity type." });

        var activity = new Activity
        {
            UserId = GetUserId(),
            ContactId = request.ContactId,
            DealId = request.DealId,
            Type = type,
            Subject = request.Subject,
            Body = request.Body,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow
        };

        await _activities.CreateAsync(activity);
        var created = await _activities.GetByIdAsync(activity.Id);
        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, MapActivityDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateActivityRequest request)
    {
        var activity = await _activities.GetByIdAsync(id);
        if (activity == null) return NotFound();

        if (Enum.TryParse<ActivityType>(request.Type, true, out var type))
            activity.Type = type;

        activity.Subject = request.Subject;
        activity.Body = request.Body;
        activity.ContactId = request.ContactId;
        activity.DealId = request.DealId;
        activity.OccurredAt = request.OccurredAt ?? activity.OccurredAt;

        await _activities.UpdateAsync(activity);
        return Ok(MapActivityDto(activity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _activities.ExistsAsync(id))
            return NotFound();

        await _activities.DeleteAsync(id);
        return NoContent();
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ActivityDto MapActivityDto(Activity a) => new()
    {
        Id = a.Id,
        Type = a.Type.ToString(),
        Subject = a.Subject,
        Body = a.Body,
        AiSummary = a.AiSummary,
        ContactId = a.ContactId,
        ContactName = a.Contact != null ? $"{a.Contact.FirstName} {a.Contact.LastName}" : null,
        DealId = a.DealId,
        DealTitle = a.Deal?.Title,
        UserName = a.User?.Name ?? "",
        OccurredAt = a.OccurredAt,
        CreatedAt = a.CreatedAt
    };
}
