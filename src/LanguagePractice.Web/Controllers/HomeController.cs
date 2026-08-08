using System.Diagnostics;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ISignalingStatsClient _signaling;

    public HomeController(ApplicationDbContext db, ISignalingStatsClient signaling)
    {
        _db = db;
        _signaling = signaling;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var signaling = await _signaling.GetStatsAsync(cancellationToken);
        var recentUsers = await _db.Users.CountAsync(
            x => !x.IsBanned && x.LastLoginAtUtc != null && x.LastLoginAtUtc >= DateTime.UtcNow.AddMinutes(-30),
            cancellationToken);
        var recentGuests = await _db.GuestSessions.CountAsync(
            x => x.IsActive && x.ExpiresAtUtc > DateTime.UtcNow && x.LastSeenAtUtc != null && x.LastSeenAtUtc >= DateTime.UtcNow.AddMinutes(-30),
            cancellationToken);

        ViewBag.OnlineCount = Math.Max(signaling.ActiveConnections, recentUsers + recentGuests);
        ViewBag.QueuedCount = signaling.QueuedTotal;
        ViewBag.LangCounts = signaling.ActiveByLanguage;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
