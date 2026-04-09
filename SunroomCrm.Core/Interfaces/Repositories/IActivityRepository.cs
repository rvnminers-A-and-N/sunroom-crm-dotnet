using SunroomCrm.Core.DTOs.Activities;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(int id);
    Task<(List<Activity> Items, int Total)> GetAllAsync(int userId, ActivityFilterParams filter);
    Task<Activity> CreateAsync(Activity activity);
    Task UpdateAsync(Activity activity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<List<Activity>> GetRecentAsync(int userId, int count = 10);
    Task<List<Activity>> GetForDealAsync(int dealId);
}
