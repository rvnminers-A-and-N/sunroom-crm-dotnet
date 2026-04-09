using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunroomCrm.Core.DTOs.Dashboard;
using SunroomCrm.Core.Interfaces.Repositories;

namespace SunroomCrm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IContactRepository _contacts;
    private readonly ICompanyRepository _companies;
    private readonly IDealRepository _deals;
    private readonly IActivityRepository _activities;

    public DashboardController(
        IContactRepository contacts,
        ICompanyRepository companies,
        IDealRepository deals,
        IActivityRepository activities)
    {
        _contacts = contacts;
        _companies = companies;
        _deals = deals;
        _activities = activities;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();

        var contactCount = await _contacts.GetCountAsync(userId);
        var stageStats = await _deals.GetStageStatsAsync(userId);
        var wonRevenue = await _deals.GetWonRevenueAsync(userId);
        var recentActivities = await _activities.GetRecentAsync(userId, 10);

        var companyPagination = new Core.DTOs.Common.PaginationParams { PerPage = 1 };
        var (_, companyTotal) = await _companies.GetAllAsync(userId, null, companyPagination);

        var dealFilter = new Core.DTOs.Deals.DealFilterParams { PerPage = 1 };
        var (_, dealTotal) = await _deals.GetAllAsync(userId, dealFilter);

        return Ok(new DashboardDto
        {
            TotalContacts = contactCount,
            TotalCompanies = companyTotal,
            TotalDeals = dealTotal,
            TotalPipelineValue = stageStats
                .Where(s => s.Key != Core.Enums.DealStage.Won && s.Key != Core.Enums.DealStage.Lost)
                .Sum(s => s.Value.Total),
            WonRevenue = wonRevenue,
            DealsByStage = stageStats.Select(s => new DealStageCount
            {
                Stage = s.Key.ToString(),
                Count = s.Value.Count,
                TotalValue = s.Value.Total
            }).ToList(),
            RecentActivities = recentActivities.Select(a => new RecentActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Subject = a.Subject,
                ContactName = a.Contact != null ? $"{a.Contact.FirstName} {a.Contact.LastName}" : null,
                UserName = a.User?.Name ?? "",
                OccurredAt = a.OccurredAt
            }).ToList()
        });
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
