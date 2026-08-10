using System.Security.Claims;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Controllers.Api;

[ApiController]
[Route("api/social")]
public class SocialApiController : ControllerBase
{
    private readonly ISocialService _social;
    private readonly ApplicationDbContext _db;
    private readonly IGuestSessionService _guests;

    public SocialApiController(ISocialService social, ApplicationDbContext db, IGuestSessionService guests)
    {
        _social = social;
        _db = db;
        _guests = guests;
    }

    [Authorize]
    [HttpPost("friends/request")]
    public async Task<IActionResult> RequestFriend([FromBody] FriendRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || string.IsNullOrWhiteSpace(dto.UserId))
        {
            return BadRequest();
        }

        var row = await _social.RequestFriendAsync(userId, dto.UserId, cancellationToken);
        return row is null ? BadRequest(new { error = "Cannot send request." }) : Ok(new { id = row.Id, status = row.Status.ToString() });
    }

    [Authorize]
    [HttpPost("friends/{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] FriendRespondDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var ok = await _social.RespondFriendAsync(userId, id, dto.Accept, cancellationToken);
        return ok ? Ok() : NotFound();
    }

    [Authorize]
    [HttpPost("block")]
    public async Task<IActionResult> Block([FromBody] BlockDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        await _social.BlockAsync(userId, dto.UserId, dto.GuestSessionId, dto.Reason, cancellationToken);
        return Ok();
    }

    [Authorize]
    [HttpPost("matches/{roomId}/rate")]
    [EnableRateLimiting("reports")]
    public async Task<IActionResult> Rate(string roomId, [FromBody] RateDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var ok = await _social.RateMatchAsync(userId, roomId, dto.Rating, cancellationToken);
        return ok ? Ok() : BadRequest();
    }

    [HttpPost("matches/{roomId}/complete")]
    public async Task<IActionResult> Complete(string roomId, [FromBody] CompleteDto dto, CancellationToken cancellationToken)
    {
        string? userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        Guid? guestId = null;
        var guestToken = Request.Cookies[GuestController.GuestTokenCookie];
        if (!string.IsNullOrWhiteSpace(guestToken))
        {
            var guest = await _guests.ValidateAsync(guestToken, cancellationToken);
            guestId = guest?.Id;
        }

        var ok = await _social.CompleteMatchAsync(userId, guestId, roomId, dto.DurationSeconds, cancellationToken);
        return ok ? Ok() : NotFound();
    }

    [HttpGet("topics")]
    [ResponseCache(Duration = 60)]
    public async Task<IActionResult> Topics([FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var ui = (lang ?? "en").Split('-')[0].ToLowerInvariant();
        var rows = await _db.ConversationTopics.AsNoTracking()
            .Where(x => x.IsActive && (x.LanguageCode == "*" || x.LanguageCode == ui))
            .OrderBy(x => x.SortOrder)
            .Select(x => new
            {
                x.Id,
                text = ui == "tr" ? x.TextTr : x.TextEn
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }
}

public record FriendRequestDto(string UserId);
public record FriendRespondDto(bool Accept);
public record BlockDto(string? UserId, Guid? GuestSessionId, string? Reason);
public record RateDto(byte Rating);
public record CompleteDto(int DurationSeconds);
