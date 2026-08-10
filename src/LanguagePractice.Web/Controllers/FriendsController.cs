using System.Security.Claims;
using LanguagePractice.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguagePractice.Web.Controllers;

[Authorize]
public class FriendsController : Controller
{
    private readonly ISocialService _social;

    public FriendsController(ISocialService social)
    {
        _social = social;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var friends = await _social.GetFriendsAsync(userId, cancellationToken);
        var pending = await _social.GetPendingAsync(userId, cancellationToken);
        ViewBag.Friends = friends;
        ViewBag.Pending = pending;
        ViewBag.UserId = userId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(Guid id, bool accept, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _social.RespondFriendAsync(userId, id, accept, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Rematch(string userId, string? lang = null)
    {
        return RedirectToAction("Lobby", "Match", new { lang, auto = true, rematch = userId });
    }
}
