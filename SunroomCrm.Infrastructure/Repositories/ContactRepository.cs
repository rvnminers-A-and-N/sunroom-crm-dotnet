using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;

    public ContactRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Contact?> GetByIdAsync(int id)
        => await _db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Contact?> GetByIdWithDetailsAsync(int id)
        => await _db.Contacts
            .Include(c => c.Company)
            .Include(c => c.Tags)
            .Include(c => c.Deals)
                .ThenInclude(d => d.Company)
            .Include(c => c.Activities)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(List<Contact> Items, int Total)> GetAllAsync(
        int userId, ContactFilterParams filter)
    {
        var query = _db.Contacts
            .Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        if (filter.CompanyId.HasValue)
            query = query.Where(c => c.CompanyId == filter.CompanyId.Value);

        if (filter.TagId.HasValue)
            query = query.Where(c => c.Tags.Any(t => t.Id == filter.TagId.Value));

        var total = await query.CountAsync();

        query = filter.Sort?.ToLower() switch
        {
            "firstname" => filter.Direction == "desc"
                ? query.OrderByDescending(c => c.FirstName)
                : query.OrderBy(c => c.FirstName),
            "lastname" => filter.Direction == "desc"
                ? query.OrderByDescending(c => c.LastName)
                : query.OrderBy(c => c.LastName),
            "email" => filter.Direction == "desc"
                ? query.OrderByDescending(c => c.Email)
                : query.OrderBy(c => c.Email),
            "lastcontacted" => filter.Direction == "desc"
                ? query.OrderByDescending(c => c.LastContactedAt)
                : query.OrderBy(c => c.LastContactedAt),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        var items = await query
            .Include(c => c.Company)
            .Include(c => c.Tags)
            .Skip((filter.Page - 1) * filter.PerPage)
            .Take(filter.PerPage)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Contact> CreateAsync(Contact contact)
    {
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync();
        return contact;
    }

    public async Task UpdateAsync(Contact contact)
    {
        _db.Contacts.Update(contact);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var contact = await _db.Contacts.FindAsync(id);
        if (contact != null)
        {
            _db.Contacts.Remove(contact);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _db.Contacts.AnyAsync(c => c.Id == id);

    public async Task SyncTagsAsync(int contactId, List<int> tagIds)
    {
        var contact = await _db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == contactId);

        if (contact == null) return;

        var tags = await _db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync();
        contact.Tags.Clear();
        foreach (var tag in tags)
            contact.Tags.Add(tag);

        await _db.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync(int userId)
        => await _db.Contacts.CountAsync(c => c.UserId == userId);
}
