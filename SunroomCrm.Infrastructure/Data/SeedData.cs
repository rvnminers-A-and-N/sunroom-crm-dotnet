using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        // Users
        var admin = new User
        {
            Name = "Austin Sunroom",
            Email = "admin@sunroomcrm.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Admin
        };
        var manager = new User
        {
            Name = "Sarah Manager",
            Email = "sarah@sunroomcrm.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.Manager
        };
        var user = new User
        {
            Name = "Jake Sales",
            Email = "jake@sunroomcrm.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = UserRole.User
        };

        db.Users.AddRange(admin, manager, user);
        await db.SaveChangesAsync();

        // Tags
        var tags = new[]
        {
            new Tag { Name = "VIP", Color = "#F76C6C" },
            new Tag { Name = "Hot Lead", Color = "#F9A66C" },
            new Tag { Name = "Decision Maker", Color = "#F4C95D" },
            new Tag { Name = "Referral", Color = "#02795F" },
            new Tag { Name = "Follow Up", Color = "#3B82F6" },
            new Tag { Name = "Cold", Color = "#6B7280" }
        };
        db.Tags.AddRange(tags);
        await db.SaveChangesAsync();

        // Companies
        var companies = new[]
        {
            new Company { UserId = admin.Id, Name = "Acme Corporation", Industry = "Technology", Website = "https://acme.example.com", Phone = "(555) 100-1000", City = "Austin", State = "TX", Zip = "78701", Address = "100 Congress Ave" },
            new Company { UserId = admin.Id, Name = "Global Dynamics", Industry = "Consulting", Website = "https://globaldyn.example.com", Phone = "(555) 200-2000", City = "Dallas", State = "TX", Zip = "75201", Address = "200 Main St" },
            new Company { UserId = admin.Id, Name = "Initech Solutions", Industry = "Software", Website = "https://initech.example.com", Phone = "(555) 300-3000", City = "San Antonio", State = "TX", Zip = "78205", Address = "300 River Walk" },
            new Company { UserId = admin.Id, Name = "Stark Industries", Industry = "Manufacturing", Website = "https://stark.example.com", Phone = "(555) 400-4000", City = "Houston", State = "TX", Zip = "77002", Address = "400 Market Square" },
            new Company { UserId = manager.Id, Name = "Wayne Enterprises", Industry = "Finance", Website = "https://wayne.example.com", Phone = "(555) 500-5000", City = "Fort Worth", State = "TX", Zip = "76102", Address = "500 Sundance Square" }
        };
        db.Companies.AddRange(companies);
        await db.SaveChangesAsync();

        // Contacts
        var contacts = new[]
        {
            new Contact { UserId = admin.Id, CompanyId = companies[0].Id, FirstName = "John", LastName = "Smith", Email = "john@acme.example.com", Phone = "(555) 101-0001", Title = "CTO", Notes = "Key technical decision maker" },
            new Contact { UserId = admin.Id, CompanyId = companies[0].Id, FirstName = "Emily", LastName = "Chen", Email = "emily@acme.example.com", Phone = "(555) 101-0002", Title = "VP Engineering" },
            new Contact { UserId = admin.Id, CompanyId = companies[1].Id, FirstName = "Michael", LastName = "Johnson", Email = "michael@globaldyn.example.com", Phone = "(555) 201-0001", Title = "Managing Partner", Notes = "Met at conference" },
            new Contact { UserId = admin.Id, CompanyId = companies[2].Id, FirstName = "Sarah", LastName = "Williams", Email = "sarah@initech.example.com", Phone = "(555) 301-0001", Title = "Head of Product" },
            new Contact { UserId = admin.Id, CompanyId = companies[3].Id, FirstName = "David", LastName = "Brown", Email = "david@stark.example.com", Phone = "(555) 401-0001", Title = "Procurement Director" },
            new Contact { UserId = admin.Id, FirstName = "Lisa", LastName = "Davis", Email = "lisa@freelance.example.com", Phone = "(555) 601-0001", Title = "Independent Consultant", Notes = "No company affiliation" },
            new Contact { UserId = manager.Id, CompanyId = companies[4].Id, FirstName = "Robert", LastName = "Wilson", Email = "robert@wayne.example.com", Phone = "(555) 501-0001", Title = "CFO" },
            new Contact { UserId = manager.Id, CompanyId = companies[4].Id, FirstName = "Jennifer", LastName = "Taylor", Email = "jennifer@wayne.example.com", Phone = "(555) 501-0002", Title = "VP Operations" }
        };
        db.Contacts.AddRange(contacts);
        await db.SaveChangesAsync();

        // Tag assignments
        contacts[0].Tags.Add(tags[0]); // VIP
        contacts[0].Tags.Add(tags[2]); // Decision Maker
        contacts[1].Tags.Add(tags[1]); // Hot Lead
        contacts[2].Tags.Add(tags[3]); // Referral
        contacts[3].Tags.Add(tags[1]); // Hot Lead
        contacts[3].Tags.Add(tags[4]); // Follow Up
        contacts[4].Tags.Add(tags[2]); // Decision Maker
        contacts[5].Tags.Add(tags[5]); // Cold
        contacts[6].Tags.Add(tags[0]); // VIP
        contacts[7].Tags.Add(tags[4]); // Follow Up
        await db.SaveChangesAsync();

        // Deals
        var deals = new[]
        {
            new Deal { UserId = admin.Id, ContactId = contacts[0].Id, CompanyId = companies[0].Id, Title = "Acme Platform License", Value = 85000m, Stage = DealStage.Negotiation, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Notes = "Enterprise license deal, 3-year contract" },
            new Deal { UserId = admin.Id, ContactId = contacts[1].Id, CompanyId = companies[0].Id, Title = "Acme Support Package", Value = 24000m, Stage = DealStage.Proposal, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)) },
            new Deal { UserId = admin.Id, ContactId = contacts[2].Id, CompanyId = companies[1].Id, Title = "Global Dynamics Consulting", Value = 120000m, Stage = DealStage.Qualified, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)) },
            new Deal { UserId = admin.Id, ContactId = contacts[3].Id, CompanyId = companies[2].Id, Title = "Initech Integration Project", Value = 45000m, Stage = DealStage.Lead, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)) },
            new Deal { UserId = admin.Id, ContactId = contacts[4].Id, CompanyId = companies[3].Id, Title = "Stark Manufacturing Suite", Value = 250000m, Stage = DealStage.Won, ClosedAt = DateTime.UtcNow.AddDays(-15), Notes = "Largest deal this quarter!" },
            new Deal { UserId = admin.Id, ContactId = contacts[5].Id, Title = "Freelance Advisory", Value = 15000m, Stage = DealStage.Lost, ClosedAt = DateTime.UtcNow.AddDays(-7), Notes = "Lost to competitor pricing" },
            new Deal { UserId = manager.Id, ContactId = contacts[6].Id, CompanyId = companies[4].Id, Title = "Wayne Financial Suite", Value = 175000m, Stage = DealStage.Proposal, ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) }
        };
        db.Deals.AddRange(deals);
        await db.SaveChangesAsync();

        // Activities
        var activities = new[]
        {
            new Activity { UserId = admin.Id, ContactId = contacts[0].Id, DealId = deals[0].Id, Type = ActivityType.Meeting, Subject = "Initial Discovery Meeting", Body = "Discussed pain points with current platform. They need better reporting and API integration. Budget approved for Q2.", OccurredAt = DateTime.UtcNow.AddDays(-14) },
            new Activity { UserId = admin.Id, ContactId = contacts[0].Id, DealId = deals[0].Id, Type = ActivityType.Call, Subject = "Follow-up Call", Body = "Reviewed proposal draft. John requested additional security compliance documentation.", OccurredAt = DateTime.UtcNow.AddDays(-7) },
            new Activity { UserId = admin.Id, ContactId = contacts[0].Id, DealId = deals[0].Id, Type = ActivityType.Email, Subject = "Sent Security Compliance Docs", Body = "Emailed SOC2 and GDPR compliance documentation as requested.", OccurredAt = DateTime.UtcNow.AddDays(-5) },
            new Activity { UserId = admin.Id, ContactId = contacts[1].Id, DealId = deals[1].Id, Type = ActivityType.Meeting, Subject = "Support Package Demo", Body = "Walked through premium support SLA and response time guarantees.", OccurredAt = DateTime.UtcNow.AddDays(-3) },
            new Activity { UserId = admin.Id, ContactId = contacts[2].Id, DealId = deals[2].Id, Type = ActivityType.Call, Subject = "Qualification Call", Body = "Michael confirmed budget range and decision timeline. Need to schedule technical deep-dive.", OccurredAt = DateTime.UtcNow.AddDays(-10) },
            new Activity { UserId = admin.Id, ContactId = contacts[3].Id, Type = ActivityType.Note, Subject = "Research Notes", Body = "Initech is evaluating three vendors. Our differentiator is the integration API.", OccurredAt = DateTime.UtcNow.AddDays(-8) },
            new Activity { UserId = admin.Id, ContactId = contacts[4].Id, DealId = deals[4].Id, Type = ActivityType.Meeting, Subject = "Contract Signing", Body = "Finalized contract terms. 3-year deal signed. Implementation starts next month.", OccurredAt = DateTime.UtcNow.AddDays(-15) },
            new Activity { UserId = admin.Id, Type = ActivityType.Task, Subject = "Prepare Q2 Pipeline Report", Body = "Compile pipeline metrics and forecast for leadership review.", OccurredAt = DateTime.UtcNow.AddDays(-1) },
            new Activity { UserId = manager.Id, ContactId = contacts[6].Id, DealId = deals[6].Id, Type = ActivityType.Meeting, Subject = "Financial Suite Requirements", Body = "Robert outlined the requirements for their financial reporting modernization project.", OccurredAt = DateTime.UtcNow.AddDays(-5) },
            new Activity { UserId = manager.Id, ContactId = contacts[7].Id, Type = ActivityType.Call, Subject = "Operations Check-in", Body = "Jennifer is interested in the operations module as an add-on.", OccurredAt = DateTime.UtcNow.AddDays(-2) }
        };
        db.Activities.AddRange(activities);
        await db.SaveChangesAsync();

        // AI Insights
        var insights = new[]
        {
            new AiInsight { DealId = deals[0].Id, Insight = "This deal is in the negotiation phase with strong buying signals. Recommend: 1) Send revised pricing with volume discount, 2) Schedule executive-level meeting, 3) Prepare implementation timeline.", GeneratedAt = DateTime.UtcNow.AddDays(-2) },
            new AiInsight { DealId = deals[6].Id, Insight = "Wayne Enterprises has a large budget and clear requirements. Recommend: 1) Fast-track the proposal, 2) Offer pilot program, 3) Arrange reference call with similar financial institution.", GeneratedAt = DateTime.UtcNow.AddDays(-1) }
        };
        db.AiInsights.AddRange(insights);
        await db.SaveChangesAsync();
    }
}
