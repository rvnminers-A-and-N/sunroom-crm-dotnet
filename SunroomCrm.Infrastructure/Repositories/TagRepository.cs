using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;

    public TagRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Tag?> GetByIdAsync(int id)
        => await _db.Tags.FindAsync(id);

    public async Task<List<Tag>> GetAllAsync()
        => await _db.Tags.OrderBy(t => t.Name).ToListAsync();

    public async Task<Tag> CreateAsync(Tag tag)
    {
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    public async Task UpdateAsync(Tag tag)
    {
        _db.Tags.Update(tag);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag != null)
        {
            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _db.Tags.AnyAsync(t => t.Id == id);

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        var query = _db.Tags.Where(t => t.Name == name);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<List<Tag>> GetByIdsAsync(List<int> ids)
        => await _db.Tags.Where(t => ids.Contains(t.Id)).ToListAsync();
}
