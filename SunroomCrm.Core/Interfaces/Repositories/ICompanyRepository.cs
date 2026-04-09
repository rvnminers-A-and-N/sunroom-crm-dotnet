using SunroomCrm.Core.DTOs.Common;
using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(int id);
    Task<Company?> GetByIdWithDetailsAsync(int id);
    Task<(List<Company> Items, int Total)> GetAllAsync(int userId, string? search, PaginationParams pagination);
    Task<Company> CreateAsync(Company company);
    Task UpdateAsync(Company company);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
