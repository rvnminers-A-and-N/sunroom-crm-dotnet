using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Deals;

namespace SunroomCrm.Core.DTOs.Companies;

public class CompanyDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ContactDto> Contacts { get; set; } = new();
    public List<DealDto> Deals { get; set; } = new();
}
