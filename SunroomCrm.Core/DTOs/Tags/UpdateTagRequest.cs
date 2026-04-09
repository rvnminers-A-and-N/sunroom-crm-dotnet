using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.Tags;

public class UpdateTagRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(7)]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex code (e.g., #02795F)")]
    public string Color { get; set; } = "#02795F";
}
