using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.Deals;

public class CreateDealRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Range(0, 999999999999.99)]
    public decimal Value { get; set; }

    [Required]
    public int ContactId { get; set; }

    public int? CompanyId { get; set; }

    public string? Stage { get; set; } = "Lead";

    public DateOnly? ExpectedCloseDate { get; set; }

    public string? Notes { get; set; }
}
