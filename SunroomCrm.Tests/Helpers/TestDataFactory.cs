using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Tests.Helpers;

public static class TestDataFactory
{
    public static User CreateUser(
        string name = "Test User",
        string email = "test@example.com",
        UserRole role = UserRole.User)
    {
        return new User
        {
            Name = name,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = role
        };
    }

    public static Company CreateCompany(int userId, string name = "Test Company")
    {
        return new Company
        {
            UserId = userId,
            Name = name,
            Industry = "Technology",
            City = "Austin",
            State = "TX"
        };
    }

    public static Contact CreateContact(
        int userId,
        int? companyId = null,
        string firstName = "John",
        string lastName = "Doe")
    {
        return new Contact
        {
            UserId = userId,
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}.{lastName.ToLower()}@example.com",
            Title = "Engineer"
        };
    }

    public static Deal CreateDeal(
        int userId,
        int contactId,
        int? companyId = null,
        string title = "Test Deal",
        decimal value = 10000m,
        DealStage stage = DealStage.Lead)
    {
        return new Deal
        {
            UserId = userId,
            ContactId = contactId,
            CompanyId = companyId,
            Title = title,
            Value = value,
            Stage = stage
        };
    }

    public static Activity CreateActivity(
        int userId,
        int? contactId = null,
        int? dealId = null,
        ActivityType type = ActivityType.Note,
        string subject = "Test Activity")
    {
        return new Activity
        {
            UserId = userId,
            ContactId = contactId,
            DealId = dealId,
            Type = type,
            Subject = subject,
            Body = "Test activity body content",
            OccurredAt = DateTime.UtcNow
        };
    }

    public static Tag CreateTag(string name = "Test Tag", string color = "#02795F")
    {
        return new Tag { Name = name, Color = color };
    }
}
