using SunroomCrm.Core.DTOs.Contacts;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(int id);
    Task<Contact?> GetByIdWithDetailsAsync(int id);
    Task<(List<Contact> Items, int Total)> GetAllAsync(int userId, ContactFilterParams filter);
    Task<Contact> CreateAsync(Contact contact);
    Task UpdateAsync(Contact contact);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task SyncTagsAsync(int contactId, List<int> tagIds);
    Task<int> GetCountAsync(int userId);
}
