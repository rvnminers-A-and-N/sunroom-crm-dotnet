using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.Contacts;

public class UpdateContactRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Title { get; set; }

    public string? Notes { get; set; }

    public int? CompanyId { get; set; }
}
