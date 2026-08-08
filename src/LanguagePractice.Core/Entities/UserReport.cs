using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Kullanıcı / misafir şikayet kaydı (moderasyon).
/// </summary>
public class UserReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ReporterUserId { get; set; }
    public Guid? ReporterGuestSessionId { get; set; }
    public string? ReportedUserId { get; set; }
    public Guid? ReportedGuestSessionId { get; set; }
    public string? ReportedPeerSocketId { get; set; }
    public string? ReportedPeerDisplayName { get; set; }
    public string? RoomId { get; set; }
    public Guid? MatchHistoryId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedByAdminId { get; set; }
    public string? AdminNotes { get; set; }
    public string? AutoAction { get; set; }
    public string? ReporterIpHash { get; set; }

    public ApplicationUser? ReporterUser { get; set; }
    public ApplicationUser? ReportedUser { get; set; }
    public GuestSession? ReporterGuestSession { get; set; }
    public GuestSession? ReportedGuestSession { get; set; }
    public MatchHistory? MatchHistory { get; set; }
}
