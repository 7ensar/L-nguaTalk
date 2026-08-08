using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Eşleşme ve görüşme geçmişi kaydı.
/// </summary>
public class MatchHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserAId { get; set; }
    public string? UserBId { get; set; }
    public Guid? GuestSessionAId { get; set; }
    public Guid? GuestSessionBId { get; set; }
    public int PracticeLanguageId { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public MatchStatus Status { get; set; } = MatchStatus.Queued;
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? MatchedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public byte? RatingByUserA { get; set; }
    public byte? RatingByUserB { get; set; }
    public string? Notes { get; set; }

    public ApplicationUser? UserA { get; set; }
    public ApplicationUser? UserB { get; set; }
    public GuestSession? GuestSessionA { get; set; }
    public GuestSession? GuestSessionB { get; set; }
    public Language PracticeLanguage { get; set; } = null!;
}
