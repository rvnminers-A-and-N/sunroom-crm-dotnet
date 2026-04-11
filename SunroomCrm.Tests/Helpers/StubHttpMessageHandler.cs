using System.Net;

namespace SunroomCrm.Tests.Helpers;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/> that lets a unit test
/// inspect the outbound request and return a canned response (or throw).
/// Used to test <see cref="SunroomCrm.Infrastructure.Services.OllamaAiService"/>
/// without actually contacting Ollama.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    /// <summary>Always returns the given <paramref name="json"/> body with HTTP 200.</summary>
    public static StubHttpMessageHandler ReturnsOk(string json) =>
        new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        }));

    /// <summary>Always returns an HTTP error response.</summary>
    public static StubHttpMessageHandler ReturnsError(HttpStatusCode status = HttpStatusCode.InternalServerError) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent("error", System.Text.Encoding.UTF8, "text/plain")
        }));

    /// <summary>Always throws when called.</summary>
    public static StubHttpMessageHandler Throws(Exception exception) =>
        new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content != null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }
        else
        {
            RequestBodies.Add(string.Empty);
        }
        return await _responder(request);
    }
}
