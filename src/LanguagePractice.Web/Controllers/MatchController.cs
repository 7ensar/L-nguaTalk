using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Web.Controllers;

public class MatchController : Controller
{
    private readonly IGuestSessionService _guestSessions;
    private readonly SignalingOptions _signaling;
    private readonly WebRtcOptions _webrtc;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IModerationService _moderation;
    private readonly ISocialService _social;
    private readonly ApplicationDbContext _db;

    public MatchController(
        IGuestSessionService guestSessions,
        IOptions<SignalingOptions> signaling,
        IOptions<WebRtcOptions> webrtc,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IModerationService moderation,
        ISocialService social,
        ApplicationDbContext db)
    {
        _guestSessions = guestSessions;
        _signaling = signaling.Value;
        _webrtc = webrtc.Value;
        _userManager = userManager;
        _signInManager = signInManager;
        _moderation = moderation;
        _social = social;
        _db = db;
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

            profile = await _db.Profiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        }

        if (!isMember && guest is null)
        {
            return RedirectToAction("Enter", "Guest", new { lang, returnUrl = Url.Action("Lobby", "Match", new { lang, auto = true, rematch }) });
        }

        if (guest is not null)
        {
            await _guestSessions.TouchAsync(guest.Id, cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(lang)
            ? (profile?.TargetLanguageCode ?? guest?.PreferredLanguageCode ?? "en")
            : lang.Trim().ToLowerInvariant();

        var blocked = user is null
            ? Array.Empty<string>()
            : await _social.GetBlockedUserIdsAsync(user.Id, cancellationToken);

        var topics = await _db.ConversationTopics.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Take(20)
            .Select(x => x.TextEn)
            .ToListAsync(cancellationToken);

        var isPremium = user is not null
            && user.IsPremium
            && (user.PremiumExpiresAtUtc is null || user.PremiumExpiresAtUtc > DateTime.UtcNow);

        ViewBag.SignalingUrl = _signaling.PublicUrl;
        ViewBag.DisplayName = isMember ? user!.DisplayName : guest!.DisplayName;
        ViewBag.IsGuest = !isMember;
        ViewBag.UserId = user?.Id;
        ViewBag.GuestSessionId = guest?.Id;
        ViewBag.LanguageCode = languageCode;
        ViewBag.AutoStart = auto || !string.IsNullOrWhiteSpace(lang) || !string.IsNullOrWhiteSpace(rematch);
        ViewBag.IceServers = _webrtc.IceServers;
        ViewBag.LanguageLevel = profile?.LanguageLevel is null ? (int?)null : (int)profile.LanguageLevel.Value;
        ViewBag.Gender = profile?.Gender is null ? (int?)null : (int)profile.Gender.Value;
        ViewBag.PreferredPartnerGender = profile?.PreferredPartnerGender is null
            ? (int?)null
            : (int)profile.PreferredPartnerGender.Value;
        ViewBag.Interests = (profile?.Interests ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ViewBag.PreferSimilarLevel = profile?.PreferSimilarLevel ?? true;
        ViewBag.PreferSharedInterests = profile?.PreferSharedInterests ?? true;
        ViewBag.BrowserNotifications = profile?.BrowserNotificationsEnabled ?? true;
        ViewBag.IsPremium = isPremium;
        ViewBag.BlockedUserIds = blocked;
        ViewBag.RematchWithUserId = rematch;
        ViewBag.Topics = topics;
        ViewData["HideFooter"] = true;
        ViewData["Title"] = "Lobby";

        return View();
    }
}
