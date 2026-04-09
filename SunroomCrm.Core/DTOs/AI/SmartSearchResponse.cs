using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.Contacts;

namespace SunroomCrm.Core.DTOs.AI;

public class SmartSearchResponse
{
    public string Interpretation { get; set; } = string.Empty;
    public List<ContactDto> Contacts { get; set; } = new();
    public List<ActivityDto> Activities { get; set; } = new();
}
