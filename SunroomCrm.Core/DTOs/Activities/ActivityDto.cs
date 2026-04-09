namespace SunroomCrm.Core.DTOs.Activities;

public class ActivityDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? AiSummary { get; set; }
    public int? ContactId { get; set; }
    public string? ContactName { get; set; }
    public int? DealId { get; set; }
    public string? DealTitle { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
