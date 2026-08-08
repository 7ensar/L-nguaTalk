using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Infrastructure.Repositories;

public class MatchHistoryRepository : IMatchHistoryRepository
{
    private readonly ApplicationDbContext _db;

    public MatchHistoryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MatchHistory> AddAsync(MatchHistory match, CancellationToken cancellationToken = default)
    {
        _db.MatchHistories.Add(match);
        await _db.SaveChangesAsync(cancellationToken);
        return match;
    }

    public Task<MatchHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.MatchHistories
            .Include(x => x.PracticeLanguage)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<MatchHistory?> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default)
        => _db.MatchHistories
            .Include(x => x.PracticeLanguage)
            .FirstOrDefaultAsync(x => x.RoomId == roomId, cancellationToken);

    public async Task UpdateAsync(MatchHistory match, CancellationToken cancellationToken = default)
    {
        _db.MatchHistories.Update(match);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchHistory>> GetRecentForUserAsync(
        string userId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _db.MatchHistories
            .AsNoTracking()
            .Include(x => x.PracticeLanguage)
            .Where(x => x.UserAId == userId || x.UserBId == userId)
            .OrderByDescending(x => x.QueuedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
