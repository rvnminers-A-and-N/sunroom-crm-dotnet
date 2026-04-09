using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.AI;

public class SummarizeRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
}
