using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Services;

public interface IAiService
{
    Task<string> SummarizeAsync(string text);
    Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history);
    Task<SmartSearchResponse> SmartSearchAsync(string query, List<Contact> contacts, List<Activity> activities);
}
