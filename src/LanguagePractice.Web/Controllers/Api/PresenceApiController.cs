using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Controllers.Api;

[ApiController]
[Route("api/presence")]
public class PresenceApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISignalingStatsClient _signaling;

    public PresenceApiController(ApplicationDbContext db, ISignalingStatsClient signaling)
    {
        _db = db;
        _signaling = signaling;
    }

    [HttpGet("online")]
    public async Task<IActionResult> Online(CancellationToken cancellationToken)
    {
        var signaling = await _signaling.GetStatsAsync(cancellationToken);
        var recentUsers = await _db.Users.CountAsync(
            x => !x.IsBanned && x.LastLoginAtUtc != null && x.LastLoginAtUtc >= DateTime.UtcNow.AddMinutes(-30),
            cancellationToken);
        var recentGuests = await _db.GuestSessions.CountAsync(
            x => x.IsActive && x.ExpiresAtUtc > DateTime.UtcNow && x.LastSeenAtUtc != null && x.LastSeenAtUtc >= DateTime.UtcNow.AddMinutes(-30),
            cancellationToken);

        var online = Math.Max(signaling.ActiveConnections, recentUsers + recentGuests);
        return Ok(new
        {
            online,
            queued = signaling.QueuedTotal,
            rooms = signaling.ActiveRooms,
            byLanguage = signaling.ActiveByLanguage,
            queuedByLanguage = signaling.QueuedByLanguage
        });
    }
}
