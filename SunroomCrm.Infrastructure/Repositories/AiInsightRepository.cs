using Microsoft.EntityFrameworkCore;
using SunroomCrm.Core.Entities;
using SunroomCrm.Core.Interfaces.Repositories;
using SunroomCrm.Infrastructure.Data;

namespace SunroomCrm.Infrastructure.Repositories;

public class AiInsightRepository : IAiInsightRepository
{
    private readonly AppDbContext _db;

    public AiInsightRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AiInsight>> GetForDealAsync(int dealId)
        => await _db.AiInsights
            .Where(ai => ai.DealId == dealId)
            .OrderByDescending(ai => ai.GeneratedAt)
            .ToListAsync();

    public async Task<AiInsight> CreateAsync(AiInsight insight)
    {
        _db.AiInsights.Add(insight);
        await _db.SaveChangesAsync();
        return insight;
    }
}
