using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LanguagePractice.Web.Controllers.Api;

[ApiController]
[Route("api/presence")]
[EnableRateLimiting("presence")]
public class PresenceApiController : ControllerBase
{
    private const string CacheKey = "presence:online";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(3);

    private readonly ApplicationDbContext _db;
    private readonly ISignalingStatsClient _signaling;
    private readonly IMemoryCache _cache;

    public PresenceApiController(
        ApplicationDbContext db,
        ISignalingStatsClient signaling,
        IMemoryCache cache)
    {
        _db = db;
        _signaling = signaling;
        _cache = cache;
    }

    [HttpGet("online")]
    [ResponseCache(Duration = 3, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Online(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out object? cached) && cached is not null)
        {
            return Ok(cached);
        }

        var signaling = await _signaling.GetStatsAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddMinutes(-30);

        var recentUsers = await _db.Users
            .AsNoTracking()
            .CountAsync(
                x => !x.IsBanned && x.LastLoginAtUtc != null && x.LastLoginAtUtc >= cutoff,
                cancellationToken);

        var recentGuests = await _db.GuestSessions
            .AsNoTracking()
            .CountAsync(
                x => x.IsActive && x.ExpiresAtUtc > DateTime.UtcNow
                     && x.LastSeenAtUtc != null && x.LastSeenAtUtc >= cutoff,
                cancellationToken);

        var online = Math.Max(signaling.ActiveConnections, recentUsers + recentGuests);
        var payload = new
        {
            online,
            queued = signaling.QueuedTotal,
            rooms = signaling.ActiveRooms,
            byLanguage = signaling.ActiveByLanguage,
            queuedByLanguage = signaling.QueuedByLanguage
        };

        _cache.Set(CacheKey, payload, CacheTtl);
        return Ok(payload);
    }
}
