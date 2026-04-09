using SunroomCrm.Core.DTOs.Tags;

namespace SunroomCrm.Core.DTOs.Contacts;

public class ContactDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string? CompanyName { get; set; }
    public int? CompanyId { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public List<TagDto> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
