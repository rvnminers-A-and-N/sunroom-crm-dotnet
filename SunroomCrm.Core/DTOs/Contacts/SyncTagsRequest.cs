using System.ComponentModel.DataAnnotations;

namespace SunroomCrm.Core.DTOs.Contacts;

public class SyncTagsRequest
{
    [Required]
    public List<int> TagIds { get; set; } = new();
}
