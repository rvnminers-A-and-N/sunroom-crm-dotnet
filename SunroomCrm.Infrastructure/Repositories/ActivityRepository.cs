using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _db;

    public ActivityRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Activity?> GetByIdAsync(int id)
        => await _db.Activities
            .Include(a => a.User)
            .Include(a => a.Contact)
            .Include(a => a.Deal)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<(List<Activity> Items, int Total)> GetAllAsync(
        int userId, ActivityFilterParams filter)
    {
        var query = _db.Activities
            .Where(a => a.UserId == userId);

        if (filter.ContactId.HasValue)
            query = query.Where(a => a.ContactId == filter.ContactId.Value);

        if (filter.DealId.HasValue)
            query = query.Where(a => a.DealId == filter.DealId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Type) &&
            Enum.TryParse<ActivityType>(filter.Type, true, out var type))
        {
            query = query.Where(a => a.Type == type);
        }

        var total = await query.CountAsync();

        var items = await query
            .Include(a => a.User)
            .Include(a => a.Contact)
            .Include(a => a.Deal)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((filter.Page - 1) * filter.PerPage)
            .Take(filter.PerPage)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Activity> CreateAsync(Activity activity)
    {
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        return activity;
    }

    public async Task UpdateAsync(Activity activity)
    {
        _db.Activities.Update(activity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity != null)
        {
            _db.Activities.Remove(activity);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _db.Activities.AnyAsync(a => a.Id == id);

    public async Task<List<Activity>> GetRecentAsync(int userId, int count = 10)
        => await _db.Activities
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .Include(a => a.Contact)
            .Include(a => a.Deal)
            .OrderByDescending(a => a.OccurredAt)
            .Take(count)
            .ToListAsync();

    public async Task<List<Activity>> GetForDealAsync(int dealId)
        => await _db.Activities
            .Where(a => a.DealId == dealId)
            .Include(a => a.User)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync();
}
