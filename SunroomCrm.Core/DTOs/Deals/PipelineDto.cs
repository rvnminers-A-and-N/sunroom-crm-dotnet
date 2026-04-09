namespace SunroomCrm.Core.DTOs.Deals;

public class PipelineDto
{
    public List<PipelineStageDto> Stages { get; set; } = new();
}

public class PipelineStageDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public List<DealDto> Deals { get; set; } = new();
}
