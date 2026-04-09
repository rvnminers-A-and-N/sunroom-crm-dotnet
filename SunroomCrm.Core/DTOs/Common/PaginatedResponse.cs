namespace SunroomCrm.Core.DTOs.Common;

public class PaginatedResponse<T>
{
    public List<T> Data { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

public class PaginationMeta
{
    public int CurrentPage { get; set; }
    public int PerPage { get; set; }
    public int Total { get; set; }
    public int LastPage { get; set; }
}
