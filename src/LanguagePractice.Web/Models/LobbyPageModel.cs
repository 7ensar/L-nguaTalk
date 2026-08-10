using LanguagePractice.Infrastructure.Services;

namespace LanguagePractice.Web.Models;

public sealed class LobbyPageModel
{
    public string SignalingUrl { get; set; } = "http://localhost:5050";
    public string DisplayName { get; set; } = "Guest";
    public bool IsGuest { get; set; } = true;
    public string? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string LanguageCode { get; set; } = "en";
    public bool AutoStart { get; set; }
    public bool IsPremium { get; set; }
    public bool BrowserNotifications { get; set; } = true;
    public int? LanguageLevel { get; set; }
    public int? Gender { get; set; }
    public int? PreferredPartnerGender { get; set; }
    public IReadOnlyList<string> Interests { get; set; } = Array.Empty<string>();
    public bool PreferSimilarLevel { get; set; } = true;
    public bool PreferSharedInterests { get; set; } = true;
    public IReadOnlyList<string> BlockedUserIds { get; set; } = Array.Empty<string>();
    public string? RematchWithUserId { get; set; }
    public IReadOnlyList<string> Topics { get; set; } = Array.Empty<string>();
    public IReadOnlyList<IceServerOptions> IceServers { get; set; } = Array.Empty<IceServerOptions>();
}
