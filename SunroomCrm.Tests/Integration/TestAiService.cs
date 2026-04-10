using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Deterministic AI service used by integration tests. Avoids Ollama and the
/// stub service's templated output so assertions can be exact.
/// </summary>
public class TestAiService : IAiService
{
    public const string SummaryText = "Test summary.";
    public const string InsightText = "Test insight.";
    public const string InterpretationText = "Test interpretation.";

    public Task<string> SummarizeAsync(string text)
    {
        return Task.FromResult(SummaryText);
    }

    public Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history)
    {
        return Task.FromResult(InsightText);
    }

    public Task<SmartSearchResponse> SmartSearchAsync(
        string query,
        List<Contact> contacts,
        List<Activity> activities)
    {
        return Task.FromResult(new SmartSearchResponse
        {
            Interpretation = InterpretationText,
            Contacts = contacts.Select(c => new ContactDto
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                Title = c.Title,
                CompanyId = c.CompanyId,
                CompanyName = c.Company?.Name,
                LastContactedAt = c.LastContactedAt,
                Tags = new List<TagDto>(),
                CreatedAt = c.CreatedAt
            }).ToList(),
            Activities = activities.Select(a => new ActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Subject = a.Subject,
                Body = a.Body,
                AiSummary = a.AiSummary,
                ContactId = a.ContactId,
                ContactName = a.Contact != null ? $"{a.Contact.FirstName} {a.Contact.LastName}" : null,
                DealId = a.DealId,
                DealTitle = a.Deal?.Title,
                UserName = a.User?.Name ?? string.Empty,
                OccurredAt = a.OccurredAt,
                CreatedAt = a.CreatedAt
            }).ToList()
        });
    }
}
