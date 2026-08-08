namespace LanguagePractice.Core.Interfaces;

public record AdminDashboardStats(
    int ActiveUsersLast15Min,
    int TotalRegisteredUsers,
    int BannedUsers,
    int QueuedUsers,
    int ActiveCalls,
    int TotalMatches,
    int CompletedCalls,
    int OpenReports,
    int ActiveGuestSessions,
    IReadOnlyDictionary<string, int> QueueByLanguage);

public interface IAdminDashboardService
{
    Task<AdminDashboardStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
