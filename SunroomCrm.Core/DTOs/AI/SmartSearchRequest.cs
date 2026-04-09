using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.AI;

public class SmartSearchRequest
{
    [Required]
    public string Query { get; set; } = string.Empty;
}
