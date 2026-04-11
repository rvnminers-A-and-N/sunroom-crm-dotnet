using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Infrastructure.Data;
using SunroomCrm.Tests.Helpers;

namespace SunroomCrm.Tests.Unit.Data;

public class AppDbContextTests
{
    [Fact]
    public void AppDbContext_ExposesAllExpectedDbSets()
    {
        using var db = TestDbContext.Create();

        db.Users.Should().NotBeNull();
        db.Companies.Should().NotBeNull();
        db.Contacts.Should().NotBeNull();
        db.Tags.Should().NotBeNull();
        db.Deals.Should().NotBeNull();
        db.Activities.Should().NotBeNull();
        db.AiInsights.Should().NotBeNull();
    }

    [Fact]
    public void AppDbContext_ConfiguresUserEmailUniqueIndex()
    {
        using var db = TestDbContext.Create();

        var index = db.Model.FindEntityType(typeof(User))!
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(User.Email)));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void AppDbContext_ConfiguresTagNameUniqueIndex()
    {
        using var db = TestDbContext.Create();

        var index = db.Model.FindEntityType(typeof(Tag))!
            .GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Tag.Name)));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void AppDbContext_ExposesUserRoleProperty()
    {
        using var db = TestDbContext.Create();

        var roleProperty = db.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.Role));

        roleProperty.Should().NotBeNull();
        roleProperty!.ClrType.Should().Be(typeof(UserRole));
    }

    [Fact]
    public void AppDbContext_ExposesDealStageProperty()
    {
        using var db = TestDbContext.Create();

        var stageProperty = db.Model.FindEntityType(typeof(Deal))!
            .FindProperty(nameof(Deal.Stage));

        stageProperty.Should().NotBeNull();
        stageProperty!.ClrType.Should().Be(typeof(DealStage));
    }

    [Fact]
    public void AppDbContext_ExposesActivityTypeProperty()
    {
        using var db = TestDbContext.Create();

        var typeProperty = db.Model.FindEntityType(typeof(Activity))!
            .FindProperty(nameof(Activity.Type));

        typeProperty.Should().NotBeNull();
        typeProperty!.ClrType.Should().Be(typeof(ActivityType));
    }

    [Fact]
    public async Task AppDbContext_RoundTripsUserRoleEnum()
    {
        // The relational HasConversion<string>() is a no-op under InMemory, but
        // the round-trip must still work because the runtime ClrType is the enum.
        using var db = TestDbContext.Create();
        db.Users.Add(new User
        {
            Name = "Admin",
            Email = "admin@example.com",
            Password = "x",
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();

        var loaded = await db.Users.SingleAsync(u => u.Email == "admin@example.com");
        loaded.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void AppDbContext_ConfiguresContactTagJoinTable()
    {
        using var db = TestDbContext.Create();
        var contactType = db.Model.FindEntityType(typeof(Contact))!;
        var tagsNav = contactType.GetSkipNavigations().FirstOrDefault(n => n.Name == nameof(Contact.Tags));

        tagsNav.Should().NotBeNull();
        tagsNav!.JoinEntityType.Should().NotBeNull();
    }

    [Fact]
    public void AppDbContext_ConfiguresContactCascadeDeleteFromCompany_AsSetNull()
    {
        using var db = TestDbContext.Create();
        var fk = db.Model.FindEntityType(typeof(Contact))!
            .GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(Company));

        fk.Should().NotBeNull();
        fk!.DeleteBehavior.Should().Be(DeleteBehavior.SetNull);
    }

    [Fact]
    public void AppDbContext_ConfiguresContactDeleteFromUser_AsRestrict()
    {
        using var db = TestDbContext.Create();
        var fk = db.Model.FindEntityType(typeof(Contact))!
            .GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(User));

        fk.Should().NotBeNull();
        fk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void AppDbContext_ConfiguresCompanyDeleteFromUser_AsCascade()
    {
        using var db = TestDbContext.Create();
        var fk = db.Model.FindEntityType(typeof(Company))!
            .GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(User));

        fk.Should().NotBeNull();
        fk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void AppDbContext_ConfiguresAiInsightDeleteFromDeal_AsCascade()
    {
        using var db = TestDbContext.Create();
        var fk = db.Model.FindEntityType(typeof(AiInsight))!
            .GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(Deal));

        fk.Should().NotBeNull();
        fk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public async Task SaveChangesAsync_StampsCreatedAtOnAddedUser()
    {
        using var db = TestDbContext.Create();
        var user = new User
        {
            Name = "Stamped",
            Email = "stamp@example.com",
            Password = "x",
            Role = UserRole.User,
            CreatedAt = DateTime.MinValue,
            UpdatedAt = DateTime.MinValue
        };
        var before = DateTime.UtcNow.AddSeconds(-1);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.CreatedAt.Should().BeOnOrAfter(before);
        user.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesUpdatedAtOnModifiedUser()
    {
        using var db = TestDbContext.Create();
        var user = new User { Name = "U", Email = "u@example.com", Password = "x", Role = UserRole.User };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var originalUpdated = user.UpdatedAt;
        await Task.Delay(15); // Ensure clock advances on fast machines.

        user.Name = "Renamed";
        await db.SaveChangesAsync();

        user.UpdatedAt.Should().BeAfter(originalUpdated);
    }

    [Fact]
    public async Task SaveChangesAsync_DoesNotChangeCreatedAt_OnUpdate()
    {
        using var db = TestDbContext.Create();
        var user = new User { Name = "U", Email = "u@example.com", Password = "x", Role = UserRole.User };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var originalCreated = user.CreatedAt;
        await Task.Delay(15);

        user.Name = "Renamed";
        await db.SaveChangesAsync();

        user.CreatedAt.Should().Be(originalCreated);
    }

    [Fact]
    public void SaveChanges_StampsCreatedAtOnAddedUser_Synchronous()
    {
        using var db = TestDbContext.Create();
        var user = new User
        {
            Name = "Sync",
            Email = "sync@example.com",
            Password = "x",
            Role = UserRole.User,
            CreatedAt = DateTime.MinValue,
            UpdatedAt = DateTime.MinValue
        };
        var before = DateTime.UtcNow.AddSeconds(-1);

        db.Users.Add(user);
        db.SaveChanges();

        user.CreatedAt.Should().BeOnOrAfter(before);
        user.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SaveChangesAsync_StampsContactTimestamps()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var contact = new Contact
        {
            UserId = user.Id,
            FirstName = "John",
            LastName = "Doe",
            CreatedAt = DateTime.MinValue,
            UpdatedAt = DateTime.MinValue
        };
        var before = DateTime.UtcNow.AddSeconds(-1);

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        contact.CreatedAt.Should().BeOnOrAfter(before);
        contact.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SaveChangesAsync_DoesNotTouchTagCreatedAt_OnUpdate()
    {
        // Tag has CreatedAt but no UpdatedAt; on Update there should be no
        // attempt to write a missing UpdatedAt property.
        using var db = TestDbContext.Create();
        var tag = new Tag { Name = "Original", Color = "#000000" };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        var originalCreated = tag.CreatedAt;
        await Task.Delay(15);

        tag.Color = "#FFFFFF";
        await db.SaveChangesAsync();

        tag.CreatedAt.Should().Be(originalCreated);
    }

    [Fact]
    public async Task SaveChangesAsync_LeavesUnchangedEntitiesAlone()
    {
        using var db = TestDbContext.Create();
        var user = await TestSeedHelper.SeedUserAsync(db);
        var originalUpdated = user.UpdatedAt;
        await Task.Delay(15);

        // No changes to user, but trigger SaveChanges anyway via another insert.
        db.Tags.Add(new Tag { Name = "T", Color = "#000000" });
        await db.SaveChangesAsync();

        user.UpdatedAt.Should().Be(originalUpdated);
    }
}
