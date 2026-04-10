using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SunroomCrm.Core.Interfaces.Services;
using SunroomCrm.Infrastructure.Data;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Integration;

/// <summary>
/// Boots the real ASP.NET Core pipeline in-process for integration tests.
/// Replaces the SQL Server <see cref="AppDbContext"/> with an EF InMemory
/// database (unique per factory instance), wires in the test JWT signing
/// configuration, and substitutes the AI service with a stub so tests do
/// not depend on Ollama. The startup migration + seed step in
/// <see cref="Program"/> is short-circuited by the InMemory provider.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"SunroomCrmTest_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["Jwt:Key"] = JwtTestHelper.TestSigningKey,
                ["Jwt:Issuer"] = JwtTestHelper.TestIssuer,
                ["Jwt:Audience"] = JwtTestHelper.TestAudience,
                ["Jwt:ExpirationHours"] = "1",
                ["Ollama:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server-bound AppDbContext registration and replace with InMemory.
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor is not null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Replace any AI service registration with the deterministic test stub
            // so integration tests don't try to reach Ollama.
            var aiDescriptors = services.Where(d => d.ServiceType == typeof(IAiService)).ToList();
            foreach (var descriptor in aiDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddScoped<IAiService, TestAiService>();
        });
    }

    /// <summary>
    /// Returns a fresh scoped <see cref="AppDbContext"/> for arranging or asserting
    /// on database state outside of an HTTP request.
    /// </summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
