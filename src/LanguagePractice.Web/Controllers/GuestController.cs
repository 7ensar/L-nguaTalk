using LanguagePractice.Core.Interfaces;
using LanguagePractice.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LanguagePractice.Web.Controllers;

public class GuestController : Controller
{
    public const string GuestTokenCookie = "lp_guest_token";
    private readonly IGuestSessionService _guestSessions;
    private readonly ISignalingStatsClient _signaling;

    public GuestController(IGuestSessionService guestSessions, ISignalingStatsClient signaling)
    {
        _guestSessions = guestSessions;
        _signaling = signaling;
    }

    [HttpGet]
    public async Task<IActionResult> Enter(string? lang = null, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        var stats = await _signaling.GetStatsAsync(cancellationToken);
        ViewBag.LangCounts = stats.ActiveByLanguage;
        return View(new GuestLoginViewModel
        {
            PreferredLanguageCode = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(GuestLoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var stats = await _signaling.GetStatsAsync(cancellationToken);
            ViewBag.LangCounts = stats.ActiveByLanguage;
            return View(model);
        }

        var session = await _guestSessions.CreateAsync(
            model.DisplayName,
            model.PreferredLanguageCode,
            TimeSpan.FromHours(6));

        Response.Cookies.Append(GuestTokenCookie, session.SessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = session.ExpiresAtUtc
        });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Lobby", "Match", new { lang = model.PreferredLanguageCode, auto = true });
    }
}
