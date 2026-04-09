namespace SunroomCrm.Core.Entities;

public class AiInsight
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public string Insight { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Deal Deal { get; set; } = null!;
}
