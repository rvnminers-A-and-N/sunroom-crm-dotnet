using SunroomCrm.Core.DTOs.Common;

namespace SunroomCrm.Core.DTOs.Contacts;

public class ContactFilterParams : PaginationParams
{
    public string? Search { get; set; }
    public int? CompanyId { get; set; }
    public int? TagId { get; set; }
}
