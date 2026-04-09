using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class DealRepository : IDealRepository
{
    private readonly AppDbContext _db;

    public DealRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Deal?> GetByIdAsync(int id)
        => await _db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Deal?> GetByIdWithDetailsAsync(int id)
        => await _db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .Include(d => d.Activities)
                .ThenInclude(a => a.User)
            .Include(d => d.AiInsights)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<(List<Deal> Items, int Total)> GetAllAsync(
        int userId, DealFilterParams filter)
    {
        var query = _db.Deals
            .Where(d => d.UserId == userId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Stage) &&
            Enum.TryParse<DealStage>(filter.Stage, true, out var stage))
        {
            query = query.Where(d => d.Stage == stage);
        }

        var total = await query.CountAsync();

        query = filter.Sort?.ToLower() switch
        {
            "title" => filter.Direction == "desc"
                ? query.OrderByDescending(d => d.Title)
                : query.OrderBy(d => d.Title),
            "value" => filter.Direction == "desc"
                ? query.OrderByDescending(d => d.Value)
                : query.OrderBy(d => d.Value),
            "stage" => filter.Direction == "desc"
                ? query.OrderByDescending(d => d.Stage)
                : query.OrderBy(d => d.Stage),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        var items = await query
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .Skip((filter.Page - 1) * filter.PerPage)
            .Take(filter.PerPage)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Deal> CreateAsync(Deal deal)
    {
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync();
        return deal;
    }

    public async Task UpdateAsync(Deal deal)
    {
        _db.Deals.Update(deal);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var deal = await _db.Deals.FindAsync(id);
        if (deal != null)
        {
            _db.Deals.Remove(deal);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _db.Deals.AnyAsync(d => d.Id == id);

    public async Task<List<Deal>> GetByStageAsync(int userId, DealStage stage)
        => await _db.Deals
            .Where(d => d.UserId == userId && d.Stage == stage)
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .OrderByDescending(d => d.Value)
            .ToListAsync();

    public async Task<Dictionary<DealStage, (int Count, decimal Total)>> GetStageStatsAsync(int userId)
    {
        return await _db.Deals
            .Where(d => d.UserId == userId)
            .GroupBy(d => d.Stage)
            .ToDictionaryAsync(
                g => g.Key,
                g => (g.Count(), g.Sum(d => d.Value)));
    }

    public async Task<decimal> GetWonRevenueAsync(int userId)
        => await _db.Deals
            .Where(d => d.UserId == userId && d.Stage == DealStage.Won)
            .SumAsync(d => d.Value);
}
