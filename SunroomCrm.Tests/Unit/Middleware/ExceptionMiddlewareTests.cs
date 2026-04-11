using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SunroomCrm.Api.Middleware;

namespace SunroomCrm.Tests.Unit.Middleware;

public class ExceptionMiddlewareTests
{
    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenNoExceptionThrown()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200); // Default, untouched.
    }

    [Fact]
    public async Task InvokeAsync_DoesNotWriteBody_WhenNoExceptionThrown()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        var body = await ReadBodyAsync(context);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_Returns500_WhenExceptionThrown()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_SetsJsonContentType_WhenExceptionThrown()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_WritesErrorBodyWithMessageAndDetail()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("specific failure");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        var body = await ReadBodyAsync(context);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Be("An unexpected error occurred.");
        doc.RootElement.GetProperty("detail").GetString().Should().Be("specific failure");
    }

    [Fact]
    public async Task InvokeAsync_BodyUsesCamelCaseProperties()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("oops");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        var body = await ReadBodyAsync(context);
        body.Should().Contain("\"message\"");
        body.Should().Contain("\"detail\"");
        body.Should().NotContain("\"Message\"");
        body.Should().NotContain("\"Detail\"");
    }

    [Fact]
    public async Task InvokeAsync_HandlesNullReferenceException()
    {
        RequestDelegate next = _ => throw new NullReferenceException();
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        var body = await ReadBodyAsync(context);
        body.Should().Contain("An unexpected error occurred.");
    }

    [Fact]
    public async Task InvokeAsync_HandlesArgumentException()
    {
        RequestDelegate next = _ => throw new ArgumentException("bad arg");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        var body = await ReadBodyAsync(context);
        body.Should().Contain("bad arg");
    }

    [Fact]
    public async Task InvokeAsync_DoesNotRethrow()
    {
        RequestDelegate next = _ => throw new Exception("anything");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WorksWithSyncException()
    {
        // Some delegates throw synchronously before returning a Task; the
        // try/catch must still capture them.
        RequestDelegate next = _ =>
        {
            throw new InvalidOperationException("sync throw");
#pragma warning disable CS0162
            return Task.CompletedTask;
#pragma warning restore CS0162
        };
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        var body = await ReadBodyAsync(context);
        body.Should().Contain("sync throw");
    }
}
