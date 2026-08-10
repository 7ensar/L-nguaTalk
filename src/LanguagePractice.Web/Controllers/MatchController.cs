using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Web.Controllers;

public class MatchController : Controller
{
    private static readonly string[] FallbackTopics =
    [
        "What hobby makes you lose track of time?",
        "Describe your perfect weekend.",
        "What are you learning right now besides languages?",
        "Recommend a movie or series and why.",
        "What food from your culture should everyone try?",
        "Tell a funny travel or school story.",
        "What goal are you working toward this year?",
        "If you could live anywhere for a month, where?"
    ];

    private readonly IGuestSessionService _guestSessions;
    private readonly SignalingOptions _signaling;
    private readonly WebRtcOptions _webrtc;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IModerationService _moderation;
    private readonly ISocialService _social;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MatchController> _logger;

    public MatchController(
        IGuestSessionService guestSessions,
        IOptions<SignalingOptions> signaling,
        IOptions<WebRtcOptions> webrtc,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IModerationService moderation,
        ISocialService social,
        ApplicationDbContext db,
        ILogger<MatchController> logger)
    {
        _guestSessions = guestSessions;
        _signaling = signaling.Value;
        _webrtc = webrtc.Value;
        _userManager = userManager;
        _signInManager = signInManager;
        _moderation = moderation;
        _social = social;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Lobby(
        string? lang = null,
        bool auto = false,
        string? rematch = null,
        CancellationToken cancellationToken = default)
    {
        var isMember = User.Identity?.IsAuthenticated == true;
        var guestToken = Request.Cookies[GuestController.GuestTokenCookie];
        var guest = string.IsNullOrWhiteSpace(guestToken)
            ? null
            : await _guestSessions.ValidateAsync(guestToken, cancellationToken);

        ApplicationUser? user = null;
        UserProfile? profile = null;
        if (isMember)
        {
            user = await _userManager.GetUserAsync(User);
            if (user is null || await _moderation.IsUserBannedAsync(user.Id, cancellationToken))
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account");
            }

            try
            {
                profile = await _db.Profiles.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Profile load failed for {UserId}; continuing with defaults.", user.Id);
            }
        }

        if (!isMember && guest is null)
        {
            return RedirectToAction("Enter", "Guest", new
            {
                lang,
                returnUrl = Url.Action("Lobby", "Match", new { lang, auto = true, rematch })
            });
        }

        if (guest is not null)
        {
            await _guestSessions.TouchAsync(guest.Id, cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(lang)
            ? (profile?.TargetLanguageCode ?? guest?.PreferredLanguageCode ?? "en")
            : lang.Trim().ToLowerInvariant();

        IReadOnlyList<string> blocked = Array.Empty<string>();
        if (user is not null)
        {
            try
            {
                blocked = await _social.GetBlockedUserIdsAsync(user.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blocked users load failed for {UserId}.", user.Id);
            }
        }

        var topics = await LoadTopicsAsync(cancellationToken);
        var iceServers = _webrtc.IceServers is { Count: > 0 }
            ? _webrtc.IceServers
            : new List<IceServerOptions>
            {
                new() { Urls = ["stun:stun.l.google.com:19302"] },
                new() { Urls = ["stun:stun1.l.google.com:19302"] }
            };

        var isPremium = user is not null
            && user.IsPremium
            && (user.PremiumExpiresAtUtc is null || user.PremiumExpiresAtUtc > DateTime.UtcNow);

        var model = new LobbyPageModel
        {
            SignalingUrl = string.IsNullOrWhiteSpace(_signaling.PublicUrl)
                ? "http://localhost:5050"
                : _signaling.PublicUrl,
            DisplayName = isMember ? user!.DisplayName : guest!.DisplayName,
            IsGuest = !isMember,
            UserId = user?.Id,
            GuestSessionId = guest?.Id,
            LanguageCode = languageCode,
            AutoStart = auto || !string.IsNullOrWhiteSpace(lang) || !string.IsNullOrWhiteSpace(rematch),
            IceServers = iceServers,
            LanguageLevel = profile?.LanguageLevel is null ? null : (int)profile.LanguageLevel.Value,
            Gender = profile?.Gender is null ? null : (int)profile.Gender.Value,
            PreferredPartnerGender = profile?.PreferredPartnerGender is null
                ? null
                : (int)profile.PreferredPartnerGender.Value,
            Interests = (profile?.Interests ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            PreferSimilarLevel = profile?.PreferSimilarLevel ?? true,
            PreferSharedInterests = profile?.PreferSharedInterests ?? true,
            BrowserNotifications = profile?.BrowserNotificationsEnabled ?? true,
            IsPremium = isPremium,
            BlockedUserIds = blocked,
            RematchWithUserId = rematch,
            Topics = topics
        };

        ViewData["HideFooter"] = true;
        ViewData["Title"] = "Lobby";
        return View(model);
    }

    private async Task<IReadOnlyList<string>> LoadTopicsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _db.ConversationTopics.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Take(20)
                .Select(x => x.TextEn)
                .ToListAsync(cancellationToken);
            return rows.Count > 0 ? rows : FallbackTopics;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ConversationTopics query failed; using fallback topics.");
            return FallbackTopics;
        }
    }
}
