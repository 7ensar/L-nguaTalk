namespace LanguagePractice.Core.Entities;

/// <summary>
/// Üye olmadan (misafir) pratik yapmak isteyenler için geçici oturum.
/// </summary>
public class GuestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string? PreferredLanguageCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsBanned { get; set; }
    public DateTime? BannedAtUtc { get; set; }
    public string? BanReason { get; set; }
    public DateTime? BanExpiresAtUtc { get; set; }
}
