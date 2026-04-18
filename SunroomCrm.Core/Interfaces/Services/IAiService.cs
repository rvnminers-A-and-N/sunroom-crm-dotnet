using System.Runtime.CompilerServices;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Services;

public interface IAiService
{
    Task<string> SummarizeAsync(string text);
    Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history);
    Task<SmartSearchResponse> SmartSearchAsync(string query, List<Contact> contacts, List<Activity> activities);

    IAsyncEnumerable<string> SummarizeStreamAsync(
        string text, [EnumeratorCancellation] CancellationToken ct = default);

    IAsyncEnumerable<string> GenerateDealInsightsStreamAsync(
        Deal deal, List<Activity> history, [EnumeratorCancellation] CancellationToken ct = default);

    IAsyncEnumerable<string> SmartSearchStreamAsync(
        SmartSearchRequest request, [EnumeratorCancellation] CancellationToken ct = default);
}
