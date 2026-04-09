namespace SunroomCrm.Core.DTOs.Companies;

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int ContactCount { get; set; }
    public int DealCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
