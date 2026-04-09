using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.AI;

namespace SunroomCrm.Core.DTOs.Deals;

public class DealDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public int ContactId { get; set; }
    public string? CompanyName { get; set; }
    public int? CompanyId { get; set; }
    public DateOnly? ExpectedCloseDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ActivityDto> Activities { get; set; } = new();
    public List<DealInsightDto> Insights { get; set; } = new();
}
