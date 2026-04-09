namespace SunroomCrm.Core.DTOs.Common;

public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 25;

    public int Page { get; set; } = 1;

    public int PerPage
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public string? Sort { get; set; }
    public string Direction { get; set; } = "asc";
}
