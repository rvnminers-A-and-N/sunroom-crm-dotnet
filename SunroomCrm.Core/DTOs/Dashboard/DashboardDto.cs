namespace SunroomCrm.Core.DTOs.Dashboard;

public class DashboardDto
{
    public int TotalContacts { get; set; }
    public int TotalCompanies { get; set; }
    public int TotalDeals { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WonRevenue { get; set; }
    public List<DealStageCount> DealsByStage { get; set; } = new();
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
}

public class DealStageCount
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public class RecentActivityDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
