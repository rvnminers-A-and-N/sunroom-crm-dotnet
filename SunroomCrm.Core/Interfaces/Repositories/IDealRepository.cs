using SunroomCrm.Core.DTOs.Deals;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Enums;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(int id);
    Task<Deal?> GetByIdWithDetailsAsync(int id);
    Task<(List<Deal> Items, int Total)> GetAllAsync(int userId, DealFilterParams filter);
    Task<Deal> CreateAsync(Deal deal);
    Task UpdateAsync(Deal deal);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<List<Deal>> GetByStageAsync(int userId, DealStage stage);
    Task<Dictionary<DealStage, (int Count, decimal Total)>> GetStageStatsAsync(int userId);
    Task<decimal> GetWonRevenueAsync(int userId);
}
