using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SunroomCrm.Core.DTOs.AI;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Services;

namespace SunroomCrm.Infrastructure.Services;

public class OllamaAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger<OllamaAiService> _logger;

    public OllamaAiService(HttpClient http, IConfiguration config, ILogger<OllamaAiService> logger)
    {
        _http = http;
        _baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _model = config["Ollama:Model"] ?? "llama3";
        _logger = logger;
    }

    public async Task<string> SummarizeAsync(string text)
    {
        var prompt = $"Summarize the following CRM activity notes in 2-3 concise sentences:\n\n{text}";
        return await GenerateAsync(prompt);
    }

    public async Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history)
    {
        var activitySummary = string.Join("\n",
            history.Select(a => $"- [{a.Type}] {a.Subject}: {a.Body ?? "No details"}"));

        var prompt = $"""
            Analyze this CRM deal and suggest next steps:

            Deal: {deal.Title}
            Value: ${deal.Value:N2}
            Stage: {deal.Stage}

            Recent activity:
            {activitySummary}

            Provide 3-5 actionable next steps to move this deal forward.
            """;

        return await GenerateAsync(prompt);
    }

    public async Task<SmartSearchResponse> SmartSearchAsync(
        string query, List<Contact> contacts, List<Activity> activities)
    {
        var contactList = string.Join("\n",
            contacts.Take(50).Select(c => $"- ID:{c.Id} {c.FirstName} {c.LastName} ({c.Email}) at {c.Company?.Name ?? "N/A"}"));

        var activityList = string.Join("\n",
            activities.Take(50).Select(a => $"- ID:{a.Id} [{a.Type}] {a.Subject} (Contact: {a.Contact?.FirstName} {a.Contact?.LastName})"));

        var prompt = $"""
            Given this search query: "{query}"

            Find the most relevant contacts and activities from these lists:

            Contacts:
            {contactList}

            Activities:
            {activityList}

            Return a JSON object with:
            - "interpretation": brief explanation of what the user is looking for
            - "contact_ids": array of relevant contact IDs
            - "activity_ids": array of relevant activity IDs
            """;

        var response = await GenerateAsync(prompt);

        return new SmartSearchResponse
        {
            Interpretation = response,
            Contacts = new(),
            Activities = new()
        };
    }

    private async Task<string> GenerateAsync(string prompt)
    {
        try
        {
            var request = new
            {
                model = _model,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_baseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Ollama API");
            return "AI service is currently unavailable. Please try again later.";
        }
    }
}
