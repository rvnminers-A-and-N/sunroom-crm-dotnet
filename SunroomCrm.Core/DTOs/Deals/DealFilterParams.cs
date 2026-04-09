using SunroomCrm.Core.DTOs.Common;

namespace SunroomCrm.Core.DTOs.Deals;

public class DealFilterParams : PaginationParams
{
    public string? Search { get; set; }
    public string? Stage { get; set; }
    public int? UserId { get; set; }
}
