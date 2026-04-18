using System.Runtime.CompilerServices;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.DTOs.Tags;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Infrastructure.Services;

public class StubAiService : IAiService
{
    public Task<string> SummarizeAsync(string text)
    {
        var summary = "Key discussion points covered client requirements, timeline expectations, " +
                      "and budget considerations. Follow-up meeting scheduled to finalize proposal details " +
                      "and address remaining technical questions.";
        return Task.FromResult(summary);
    }

    public Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history)
    {
        var insights = $"""
            Based on the current deal "{deal.Title}" at the {deal.Stage} stage:

            1. Schedule a follow-up call within the next 48 hours to maintain momentum
            2. Prepare a customized proposal addressing specific pain points discussed
            3. Loop in technical stakeholders for a product demo if not already done
            4. Set up a timeline with clear milestones leading to the expected close date
            5. Identify and engage the final decision maker to accelerate approval
            """;
        return Task.FromResult(insights);
    }

    public async IAsyncEnumerable<string> SummarizeStreamAsync(
        string text, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var summary = await SummarizeAsync(text);
        foreach (var word in summary.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(20, ct);
        }
    }

    public async IAsyncEnumerable<string> GenerateDealInsightsStreamAsync(
        Deal deal, List<Activity> history, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var insights = await GenerateDealInsightsAsync(deal, history);
        foreach (var word in insights.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(20, ct);
        }
    }

    public async IAsyncEnumerable<string> SmartSearchStreamAsync(
        SmartSearchRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = $"Searching for contacts and activities related to \"{request.Query}\". " +
                       "Found several matching records across your CRM data.";
        foreach (var word in response.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(20, ct);
        }
    }

    public Task<SmartSearchResponse> SmartSearchAsync(
        string query, List<Contact> contacts, List<Activity> activities)
    {
        var matchingContacts = contacts
            .Where(c =>
                c.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (c.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Company?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(10)
            .Select(c => new ContactDto
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                Title = c.Title,
                CompanyName = c.Company?.Name,
                CompanyId = c.CompanyId,
                LastContactedAt = c.LastContactedAt,
                Tags = c.Tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Color = t.Color }).ToList(),
                CreatedAt = c.CreatedAt
            })
            .ToList();

        var matchingActivities = activities
            .Where(a =>
                a.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (a.Body?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(10)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                Type = a.Type.ToString(),
                Subject = a.Subject,
                Body = a.Body,
                ContactId = a.ContactId,
                ContactName = a.Contact != null ? $"{a.Contact.FirstName} {a.Contact.LastName}" : null,
                DealId = a.DealId,
                DealTitle = a.Deal?.Title,
                UserName = a.User?.Name ?? "",
                OccurredAt = a.OccurredAt,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return Task.FromResult(new SmartSearchResponse
        {
            Interpretation = $"Searching for contacts and activities related to \"{query}\"",
            Contacts = matchingContacts,
            Activities = matchingActivities
        });
    }
}
