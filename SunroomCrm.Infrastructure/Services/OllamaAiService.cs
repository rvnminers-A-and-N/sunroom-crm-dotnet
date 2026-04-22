using System.Runtime.CompilerServices;
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

    public async IAsyncEnumerable<string> SummarizeStreamAsync(
        string text, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = $"Summarize the following CRM activity notes in 2-3 concise sentences:\n\n{text}";
        await foreach (var token in GenerateStreamAsync(prompt, ct))
            yield return token;
    }

    public async Task<string> GenerateDealInsightsAsync(Deal deal, List<Activity> history)
    {
        var prompt = BuildDealInsightsPrompt(deal, history);
        return await GenerateAsync(prompt);
    }

    public async IAsyncEnumerable<string> GenerateDealInsightsStreamAsync(
        Deal deal, List<Activity> history, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = BuildDealInsightsPrompt(deal, history);
        await foreach (var token in GenerateStreamAsync(prompt, ct))
            yield return token;
    }

    public async Task<SmartSearchResponse> SmartSearchAsync(
        string query, List<Contact> contacts, List<Activity> activities)
    {
        var prompt = BuildSmartSearchPrompt(query, contacts, activities);
        var response = await GenerateAsync(prompt);

        return new SmartSearchResponse
        {
            Interpretation = response,
            Contacts = new(),
            Activities = new()
        };
    }

    public async IAsyncEnumerable<string> SmartSearchStreamAsync(
        SmartSearchRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = BuildSmartSearchPrompt(request.Query, new List<Contact>(), new List<Activity>());
        await foreach (var token in GenerateStreamAsync(prompt, ct))
            yield return token;
    }

    private static string BuildSmartSearchPrompt(string query, List<Contact> contacts, List<Activity> activities)
    {
        var contactList = string.Join("\n",
            contacts.Take(50).Select(c => $"- ID:{c.Id} {c.FirstName} {c.LastName} ({c.Email}) at {c.Company?.Name ?? "N/A"}"));

        var activityList = string.Join("\n",
            activities.Take(50).Select(a => $"- ID:{a.Id} [{a.Type}] {a.Subject} (Contact: {a.Contact?.FirstName} {a.Contact?.LastName})"));

        return $"""
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
    }

    private static string BuildDealInsightsPrompt(Deal deal, List<Activity> history)
    {
        var activitySummary = string.Join("\n",
            history.Select(a => $"- [{a.Type}] {a.Subject}: {a.Body ?? "No details"}"));

        return $"""
            Analyze this CRM deal and suggest next steps:

            Deal: {deal.Title}
            Value: ${deal.Value:N2}
            Stage: {deal.Stage}

            Recent activity:
            {activitySummary}

            Provide 3-5 actionable next steps to move this deal forward.
            """;
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

    private async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            prompt = prompt,
            stream = true
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            Content = content
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama API for streaming");
            yield break;
        }

        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? token = null;
                bool done = false;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    token = doc.RootElement.GetProperty("response").GetString();
                    done = doc.RootElement.TryGetProperty("done", out var d) && d.GetBoolean();
                }
                catch (JsonException)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(token))
                    yield return token;

                if (done) yield break;
            }
        }
    }
}
