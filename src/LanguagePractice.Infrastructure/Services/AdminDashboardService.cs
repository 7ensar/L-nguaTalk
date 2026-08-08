using LanguagePractice.Core.Enums;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Infrastructure.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ISignalingStatsClient _signaling;

    public AdminDashboardService(ApplicationDbContext db, ISignalingStatsClient signaling)
    {
        _db = db;
        _signaling = signaling;
    }

    public async Task<AdminDashboardStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddMinutes(-15);
        var signaling = await _signaling.GetStatsAsync(cancellationToken);

        var totalUsers = await _db.Users.CountAsync(cancellationToken);
        var banned = await _db.Users.CountAsync(x => x.IsBanned, cancellationToken);
        var activeUsers = await _db.Users.CountAsync(
            x => !x.IsBanned && x.LastLoginAtUtc != null && x.LastLoginAtUtc >= since,
            cancellationToken);
        var activeGuests = await _db.GuestSessions.CountAsync(
            x => x.IsActive && x.ExpiresAtUtc > DateTime.UtcNow && x.LastSeenAtUtc != null && x.LastSeenAtUtc >= since,
            cancellationToken);

        var totalMatches = await _db.MatchHistories.CountAsync(cancellationToken);
        var completed = await _db.MatchHistories.CountAsync(x => x.Status == MatchStatus.Completed, cancellationToken);
        var activeCalls = await _db.MatchHistories.CountAsync(
            x => x.Status == MatchStatus.InCall || x.Status == MatchStatus.Matched,
            cancellationToken);
        var openReports = await _db.UserReports.CountAsync(
            x => x.Status == ReportStatus.Open || x.Status == ReportStatus.UnderReview,
            cancellationToken);

        return new AdminDashboardStats(
            ActiveUsersLast15Min: activeUsers + activeGuests,
            TotalRegisteredUsers: totalUsers,
            BannedUsers: banned,
            QueuedUsers: signaling.QueuedTotal,
            ActiveCalls: Math.Max(activeCalls, signaling.ActiveRooms),
            TotalMatches: totalMatches,
            CompletedCalls: completed,
            OpenReports: openReports,
            ActiveGuestSessions: activeGuests,
            QueueByLanguage: signaling.QueuedByLanguage);
    }
}
