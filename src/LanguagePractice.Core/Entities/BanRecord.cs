using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Geçici veya kalıcı ban kaydı (üye veya misafir).
/// </summary>
public class BanRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string? PeerKey { get; set; }
    public BanType BanType { get; set; } = BanType.Temporary;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string? CreatedByAdminId { get; set; }
    public bool IsSystemGenerated { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? RelatedReportId { get; set; }

    public ApplicationUser? User { get; set; }
    public GuestSession? GuestSession { get; set; }
    public UserReport? RelatedReport { get; set; }
}
