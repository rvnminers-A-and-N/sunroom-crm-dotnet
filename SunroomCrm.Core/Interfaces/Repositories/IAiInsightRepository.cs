using SunroomCrm.Core.Entities;

namespace SunroomCrm.Core.Interfaces.Repositories;

public interface IAiInsightRepository
{
    Task<List<AiInsight>> GetForDealAsync(int dealId);
    Task<AiInsight> CreateAsync(AiInsight insight);
}
