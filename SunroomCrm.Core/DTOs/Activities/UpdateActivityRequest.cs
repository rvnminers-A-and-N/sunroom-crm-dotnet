using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.Activities;

public class UpdateActivityRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    public string? Body { get; set; }

    public int? ContactId { get; set; }

    public int? DealId { get; set; }

    public DateTime? OccurredAt { get; set; }
}
