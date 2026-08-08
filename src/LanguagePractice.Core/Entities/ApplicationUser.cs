using Microsoft.AspNetCore.Identity;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Platform üyesi. ASP.NET Identity ile kimlik doğrulama.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsBanned { get; set; }
    public DateTime? BannedAtUtc { get; set; }
    public string? BanReason { get; set; }
    public string? BannedByAdminId { get; set; }

    public UserProfile? Profile { get; set; }
    public ICollection<UserLanguage> Languages { get; set; } = new List<UserLanguage>();
    public ICollection<MatchHistory> MatchesAsUserA { get; set; } = new List<MatchHistory>();
    public ICollection<MatchHistory> MatchesAsUserB { get; set; } = new List<MatchHistory>();
    public ICollection<UserReport> ReportsReceived { get; set; } = new List<UserReport>();
    public ICollection<BanRecord> BanRecords { get; set; } = new List<BanRecord>();
}
