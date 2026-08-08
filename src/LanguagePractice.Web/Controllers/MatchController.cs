using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Web.Controllers;

public class MatchController : Controller
{
    private readonly IGuestSessionService _guestSessions;
    private readonly SignalingOptions _signaling;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IModerationService _moderation;

    public MatchController(
        IGuestSessionService guestSessions,
        IOptions<SignalingOptions> signaling,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IModerationService moderation)
    {
        _guestSessions = guestSessions;
        _signaling = signaling.Value;
        _userManager = userManager;
        _signInManager = signInManager;
        _moderation = moderation;
    }

    [HttpGet]
    public async Task<IActionResult> Lobby(string? lang = null, bool auto = false, CancellationToken cancellationToken = default)
    {
        var isMember = User.Identity?.IsAuthenticated == true;
        var guestToken = Request.Cookies[GuestController.GuestTokenCookie];
        var guest = string.IsNullOrWhiteSpace(guestToken)
            ? null
            : await _guestSessions.ValidateAsync(guestToken, cancellationToken);

        ApplicationUser? user = null;
        if (isMember)
        {
            user = await _userManager.GetUserAsync(User);
            if (user is null || await _moderation.IsUserBannedAsync(user.Id, cancellationToken))
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account");
            }
        }

        if (!isMember && guest is null)
        {
            return RedirectToAction("Enter", "Guest", new { lang, returnUrl = Url.Action("Lobby", "Match", new { lang, auto = true }) });
        }

        if (guest is not null)
        {
            await _guestSessions.TouchAsync(guest.Id, cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(lang)
            ? (guest?.PreferredLanguageCode ?? "en")
            : lang.Trim().ToLowerInvariant();

        ViewBag.SignalingUrl = _signaling.PublicUrl;
        ViewBag.DisplayName = isMember ? user!.DisplayName : guest!.DisplayName;
        ViewBag.IsGuest = !isMember;
        ViewBag.UserId = user?.Id;
        ViewBag.GuestSessionId = guest?.Id;
        ViewBag.LanguageCode = languageCode;
        ViewBag.AutoStart = auto || !string.IsNullOrWhiteSpace(lang);
        ViewData["HideFooter"] = true;
        ViewData["Title"] = "Lobby";

        return View();
    }
}
