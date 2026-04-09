using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/deals")]
[Authorize]
public class DealsController : ControllerBase
{
    private readonly IDealRepository _deals;
    private readonly IContactRepository _contacts;

    public DealsController(IDealRepository deals, IContactRepository contacts)
    {
        _deals = deals;
        _contacts = contacts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DealFilterParams filter)
    {
        var userId = GetUserId();
        var (items, total) = await _deals.GetAllAsync(userId, filter);

        return Ok(new PaginatedResponse<DealDto>
        {
            Data = items.Select(MapDealDto).ToList(),
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
        var deal = await _deals.GetByIdWithDetailsAsync(id);
        if (deal == null) return NotFound();

        return Ok(new DealDetailDto
        {
            Id = deal.Id,
            Title = deal.Title,
            Value = deal.Value,
            Stage = deal.Stage.ToString(),
            ContactName = $"{deal.Contact.FirstName} {deal.Contact.LastName}",
            ContactId = deal.ContactId,
            CompanyName = deal.Company?.Name,
            CompanyId = deal.CompanyId,
            ExpectedCloseDate = deal.ExpectedCloseDate,
            ClosedAt = deal.ClosedAt,
            Notes = deal.Notes,
            CreatedAt = deal.CreatedAt,
            UpdatedAt = deal.UpdatedAt,
            Activities = deal.Activities
                .OrderByDescending(a => a.OccurredAt)
                .Select(a => new ActivityDto
                {
                    Id = a.Id,
                    Type = a.Type.ToString(),
                    Subject = a.Subject,
                    Body = a.Body,
                    AiSummary = a.AiSummary,
                    ContactId = a.ContactId,
                    DealId = a.DealId,
                    DealTitle = deal.Title,
                    UserName = a.User?.Name ?? "",
                    OccurredAt = a.OccurredAt,
                    CreatedAt = a.CreatedAt
                }).ToList(),
            Insights = deal.AiInsights
                .OrderByDescending(ai => ai.GeneratedAt)
                .Select(ai => new DealInsightDto
                {
                    Id = ai.Id,
                    Insight = ai.Insight,
                    GeneratedAt = ai.GeneratedAt
                }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDealRequest request)
    {
        if (!await _contacts.ExistsAsync(request.ContactId))
            return BadRequest(new { message = "Contact not found." });

        if (!Enum.TryParse<DealStage>(request.Stage, true, out var stage))
            stage = DealStage.Lead;

        var deal = new Deal
        {
            UserId = GetUserId(),
            ContactId = request.ContactId,
            CompanyId = request.CompanyId,
            Title = request.Title,
            Value = request.Value,
            Stage = stage,
            ExpectedCloseDate = request.ExpectedCloseDate,
            Notes = request.Notes
        };

        await _deals.CreateAsync(deal);
        var created = await _deals.GetByIdAsync(deal.Id);
        return CreatedAtAction(nameof(GetById), new { id = deal.Id }, MapDealDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDealRequest request)
    {
        var deal = await _deals.GetByIdAsync(id);
        if (deal == null) return NotFound();

        deal.Title = request.Title;
        deal.Value = request.Value;
        deal.ContactId = request.ContactId;
        deal.CompanyId = request.CompanyId;
        deal.ExpectedCloseDate = request.ExpectedCloseDate;
        deal.Notes = request.Notes;

        if (!string.IsNullOrWhiteSpace(request.Stage) &&
            Enum.TryParse<DealStage>(request.Stage, true, out var stage))
        {
            var oldStage = deal.Stage;
            deal.Stage = stage;

            if (stage is DealStage.Won or DealStage.Lost && oldStage != stage)
                deal.ClosedAt = DateTime.UtcNow;
            else if (stage is not DealStage.Won and not DealStage.Lost)
                deal.ClosedAt = null;
        }

        await _deals.UpdateAsync(deal);
        return Ok(MapDealDto(deal));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _deals.ExistsAsync(id))
            return NotFound();

        await _deals.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline()
    {
        var userId = GetUserId();
        var stages = new List<PipelineStageDto>();

        foreach (DealStage stage in Enum.GetValues<DealStage>())
        {
            var deals = await _deals.GetByStageAsync(userId, stage);
            stages.Add(new PipelineStageDto
            {
                Stage = stage.ToString(),
                Count = deals.Count,
                TotalValue = deals.Sum(d => d.Value),
                Deals = deals.Select(MapDealDto).ToList()
            });
        }

        return Ok(new PipelineDto { Stages = stages });
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DealDto MapDealDto(Deal d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Value = d.Value,
        Stage = d.Stage.ToString(),
        ContactName = d.Contact != null ? $"{d.Contact.FirstName} {d.Contact.LastName}" : "",
        ContactId = d.ContactId,
        CompanyName = d.Company?.Name,
        CompanyId = d.CompanyId,
        ExpectedCloseDate = d.ExpectedCloseDate,
        ClosedAt = d.ClosedAt,
        CreatedAt = d.CreatedAt
    };
}
