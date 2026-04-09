using SunroomCrm.Core.Enums;

namespace SunroomCrm.Core.Entities;

public class Activity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ContactId { get; set; }
    public int? DealId { get; set; }
    public ActivityType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? AiSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Contact? Contact { get; set; }
    public Deal? Deal { get; set; }
}
