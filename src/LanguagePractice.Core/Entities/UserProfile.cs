using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Kullanıcı profil bilgileri (bio, diller, seviye, ilgi alanları, görünürlük).
/// </summary>
public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? CountryCode { get; set; }
    public string? AvatarUrl { get; set; }
    public string? NativeLanguageCode { get; set; }
    public string? TargetLanguageCode { get; set; }
    public LanguageLevel? LanguageLevel { get; set; }

    /// <summary>
    /// Virgülle ayrılmış ilgi alanı etiketleri (ör. music,travel,movies).
    /// </summary>
    public string? Interests { get; set; }

    public bool IsDiscoverable { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
