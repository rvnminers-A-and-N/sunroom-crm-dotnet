using SunroomCrm.Core.Enums;

namespace SunroomCrm.Core.Entities;

public class Deal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ContactId { get; set; }
    public int? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DealStage Stage { get; set; } = DealStage.Lead;
    public DateOnly? ExpectedCloseDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public Company? Company { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<AiInsight> AiInsights { get; set; } = new List<AiInsight>();
}
