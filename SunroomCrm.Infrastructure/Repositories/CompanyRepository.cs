using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _db;

    public CompanyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Company?> GetByIdAsync(int id)
        => await _db.Companies.FindAsync(id);

    public async Task<Company?> GetByIdWithDetailsAsync(int id)
        => await _db.Companies
            .Include(c => c.Contacts)
            .Include(c => c.Deals)
                .ThenInclude(d => d.Contact)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(List<Company> Items, int Total)> GetAllAsync(
        int userId, string? search, PaginationParams pagination)
    {
        var query = _db.Companies
            .Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Industry != null && c.Industry.ToLower().Contains(s)) ||
                (c.City != null && c.City.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();

        query = pagination.Sort?.ToLower() switch
        {
            "name" => pagination.Direction == "desc"
                ? query.OrderByDescending(c => c.Name)
                : query.OrderBy(c => c.Name),
            "industry" => pagination.Direction == "desc"
                ? query.OrderByDescending(c => c.Industry)
                : query.OrderBy(c => c.Industry),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        var items = await query
            .Include(c => c.Contacts)
            .Include(c => c.Deals)
            .Skip((pagination.Page - 1) * pagination.PerPage)
            .Take(pagination.PerPage)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Company> CreateAsync(Company company)
    {
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        return company;
    }

    public async Task UpdateAsync(Company company)
    {
        _db.Companies.Update(company);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company != null)
        {
            _db.Companies.Remove(company);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _db.Companies.AnyAsync(c => c.Id == id);
}
