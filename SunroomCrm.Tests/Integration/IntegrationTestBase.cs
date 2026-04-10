using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Data;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Base class for integration tests. Provides:
/// <list type="bullet">
///   <item>A shared <see cref="CustomWebApplicationFactory"/> per test class via <see cref="IClassFixture{T}"/>.</item>
///   <item>Helpers for creating authenticated HTTP clients.</item>
///   <item>Helpers for seeding users and arranging baseline DB state.</item>
///   <item>JSON serializer options matching the API's camelCase policy.</item>
/// </list>
/// Each test class gets its own factory instance, which gets its own
/// uniquely-named InMemory database, so tests within a class share state but
/// tests across classes are fully isolated.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected CustomWebApplicationFactory Factory { get; }

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    /// <summary>
    /// Creates an unauthenticated HTTP client. Use for testing public endpoints
    /// (register, login) and 401 paths.
    /// </summary>
    protected HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>
    /// Creates an HTTP client with the Authorization header pre-set to a valid
    /// JWT for the given user.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(User user)
    {
        var client = Factory.CreateClient();
        var token = JwtTestHelper.GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an HTTP client whose token is already expired. Used to test that
    /// the JWT middleware rejects expired tokens with 401.
    /// </summary>
    protected HttpClient CreateClientWithExpiredToken(User user)
    {
        var client = Factory.CreateClient();
        var token = JwtTestHelper.GenerateExpiredToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an HTTP client whose token is malformed. Used to test that the
    /// JWT middleware rejects junk tokens with 401.
    /// </summary>
    protected HttpClient CreateClientWithMalformedToken()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.GenerateMalformedToken());
        return client;
    }

    /// <summary>
    /// Inserts a user directly into the test database, bypassing the register
    /// endpoint. Returns the persisted entity (with assigned Id).
    /// </summary>
    protected async Task<User> SeedUserAsync(
        string name = "Test User",
        string? email = null,
        UserRole role = UserRole.User,
        string password = "password123")
    {
        await using var db = Factory.CreateDbContext();
        var user = new User
        {
            Name = name,
            Email = email ?? $"user_{Guid.NewGuid():N}@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Resets the test database by removing all rows from every table. Call
    /// from a test's constructor or at the top of a test method when you need
    /// a clean slate.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        await using var db = Factory.CreateDbContext();
        db.AiInsights.RemoveRange(db.AiInsights);
        db.Activities.RemoveRange(db.Activities);
        db.Deals.RemoveRange(db.Deals);
        db.Contacts.RemoveRange(db.Contacts);
        db.Companies.RemoveRange(db.Companies);
        db.Tags.RemoveRange(db.Tags);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Convenience helper that GETs a JSON endpoint and deserializes the body.
    /// </summary>
    protected static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
