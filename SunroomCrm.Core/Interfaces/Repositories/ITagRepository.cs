using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(int id);
    Task<List<Tag>> GetAllAsync();
    Task<Tag> CreateAsync(Tag tag);
    Task UpdateAsync(Tag tag);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<List<Tag>> GetByIdsAsync(List<int> ids);
}
