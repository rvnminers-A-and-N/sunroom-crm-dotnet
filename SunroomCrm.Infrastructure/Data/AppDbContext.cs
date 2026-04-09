using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).HasMaxLength(255);
            entity.Property(u => u.Email).HasMaxLength(255);
            entity.Property(u => u.Password).HasMaxLength(255);
            entity.Property(u => u.AvatarUrl).HasMaxLength(255);
            entity.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        // Company
        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(255);
            entity.Property(c => c.Industry).HasMaxLength(255);
            entity.Property(c => c.Website).HasMaxLength(255);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.State).HasMaxLength(50);
            entity.Property(c => c.Zip).HasMaxLength(20);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Companies)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Contact
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.Property(c => c.FirstName).HasMaxLength(100);
            entity.Property(c => c.LastName).HasMaxLength(100);
            entity.Property(c => c.Email).HasMaxLength(255);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Title).HasMaxLength(255);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Contacts)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Company)
                .WithMany(co => co.Contacts)
                .HasForeignKey(c => c.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Tags)
                .WithMany(t => t.Contacts)
                .UsingEntity<Dictionary<string, object>>(
                    "ContactTag",
                    j => j.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Contact>().WithMany().HasForeignKey("ContactId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.ToTable("contact_tag");
                        j.HasKey("ContactId", "TagId");
                    });
        });

        // Tag
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(t => t.Name).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100);
            entity.Property(t => t.Color).HasMaxLength(7);
        });

        // Deal
        modelBuilder.Entity<Deal>(entity =>
        {
            entity.Property(d => d.Title).HasMaxLength(255);
            entity.Property(d => d.Value).HasColumnType("decimal(12,2)");
            entity.Property(d => d.Stage)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(d => d.User)
                .WithMany(u => u.Deals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Contact)
                .WithMany(c => c.Deals)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Company)
                .WithMany(co => co.Deals)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Activity
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.Property(a => a.Subject).HasMaxLength(255);
            entity.Property(a => a.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Contact)
                .WithMany(c => c.Activities)
                .HasForeignKey(a => a.ContactId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Deal)
                .WithMany(d => d.Activities)
                .HasForeignKey(a => a.DealId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AiInsight
        modelBuilder.Entity<AiInsight>(entity =>
        {
            entity.HasOne(ai => ai.Deal)
                .WithMany(d => d.AiInsights)
                .HasForeignKey(ai => ai.DealId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var now = DateTime.UtcNow;

            if (entry.Metadata.FindProperty("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = now;

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") != null)
                entry.Property("CreatedAt").CurrentValue = now;
        }
    }
}
