using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Companies;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.DTOs.Tags;

namespace SunroomCrm.Core.DTOs.Contacts;

public class ContactDetailDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public CompanyDto? Company { get; set; }
    public List<TagDto> Tags { get; set; } = new();
    public List<DealDto> Deals { get; set; } = new();
    public List<ActivityDto> Activities { get; set; } = new();
}
