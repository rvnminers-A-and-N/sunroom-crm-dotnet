namespace SunroomCrm.Core.DTOs.AI;

public class DealInsightDto
{
    public int Id { get; set; }
    public string Insight { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
