using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _ai;
    private readonly IDealRepository _deals;
    private readonly IActivityRepository _activities;
    private readonly IContactRepository _contacts;
    private readonly IAiInsightRepository _insights;

    public AiController(
        IAiService ai,
        IDealRepository deals,
        IActivityRepository activities,
        IContactRepository contacts,
        IAiInsightRepository insights)
    {
        _ai = ai;
        _deals = deals;
        _activities = activities;
        _contacts = contacts;
        _insights = insights;
    }

    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request)
    {
        var summary = await _ai.SummarizeAsync(request.Text);
        return Ok(new SummarizeResponse { Summary = summary });
    }

    [HttpPost("summarize/stream")]
    public async Task SummarizeStream([FromBody] SummarizeRequest request)
    {
        await WriteSSEStream(_ai.SummarizeStreamAsync(request.Text, HttpContext.RequestAborted));
    }

    [HttpPost("deal-insights/{dealId}")]
    public async Task<IActionResult> DealInsights(int dealId)
    {
        var deal = await _deals.GetByIdAsync(dealId);
        if (deal == null) return NotFound(new { message = "Deal not found." });

        var history = await _activities.GetForDealAsync(dealId);
        var insight = await _ai.GenerateDealInsightsAsync(deal, history);

        var aiInsight = new AiInsight
        {
            DealId = dealId,
            Insight = insight,
            GeneratedAt = DateTime.UtcNow
        };
        await _insights.CreateAsync(aiInsight);

        return Ok(new DealInsightDto
        {
            Id = aiInsight.Id,
            Insight = insight,
            GeneratedAt = aiInsight.GeneratedAt
        });
    }

    [HttpPost("deal-insights/{dealId}/stream")]
    public async Task DealInsightsStream(int dealId)
    {
        var deal = await _deals.GetByIdAsync(dealId);
        if (deal == null)
        {
            Response.StatusCode = 404;
            return;
        }

        var history = await _activities.GetForDealAsync(dealId);
        await WriteSSEStream(_ai.GenerateDealInsightsStreamAsync(deal, history, HttpContext.RequestAborted));
    }

    [HttpPost("search/stream")]
    public async Task SmartSearchStream([FromBody] SmartSearchRequest request)
    {
        await WriteSSEStream(_ai.SmartSearchStreamAsync(request, HttpContext.RequestAborted));
    }

    [HttpPost("search")]
    public async Task<IActionResult> SmartSearch([FromBody] SmartSearchRequest request)
    {
        var userId = GetUserId();

        var contactFilter = new Core.DTOs.Contacts.ContactFilterParams { PerPage = 100 };
        var (contacts, _) = await _contacts.GetAllAsync(userId, contactFilter);

        var activityFilter = new Core.DTOs.Activities.ActivityFilterParams { PerPage = 100 };
        var (activities, _) = await _activities.GetAllAsync(userId, activityFilter);

        var result = await _ai.SmartSearchAsync(request.Query, contacts, activities);
        return Ok(result);
    }

    private async Task WriteSSEStream(IAsyncEnumerable<string> tokens)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        await foreach (var token in tokens)
        {
            var data = JsonSerializer.Serialize(new { token });
            await Response.WriteAsync($"data: {data}\n\n", HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        await Response.WriteAsync("data: [DONE]\n\n", HttpContext.RequestAborted);
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
