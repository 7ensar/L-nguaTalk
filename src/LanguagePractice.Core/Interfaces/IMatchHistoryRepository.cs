using LanguagePractice.Core.Entities;

namespace LanguagePractice.Core.Interfaces;

public interface IMatchHistoryRepository
{
    Task<MatchHistory> AddAsync(MatchHistory match, CancellationToken cancellationToken = default);
    Task<MatchHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MatchHistory?> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default);
    Task UpdateAsync(MatchHistory match, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchHistory>> GetRecentForUserAsync(string userId, int take = 20, CancellationToken cancellationToken = default);
}
