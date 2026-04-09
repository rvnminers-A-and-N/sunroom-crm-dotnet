using SunroomCrm.Core.DTOs.Common;

namespace SunroomCrm.Core.DTOs.Activities;

public class ActivityFilterParams : PaginationParams
{
    public int? ContactId { get; set; }
    public int? DealId { get; set; }
    public string? Type { get; set; }
}
