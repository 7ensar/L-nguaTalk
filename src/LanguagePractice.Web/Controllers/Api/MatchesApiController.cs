using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Controllers.Api;

[ApiController]
[Route("api/matches")]
public class MatchesApiController : ControllerBase
{
    private readonly IMatchHistoryRepository _matches;
    private readonly ApplicationDbContext _db;

    public MatchesApiController(IMatchHistoryRepository matches, ApplicationDbContext db)
    {
        _matches = matches;
        _db = db;
    }

    /// <summary>
    /// Signaling servisi eşleşme oluştuğunda bu endpoint'e yazabilir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMatchRequest request, CancellationToken cancellationToken)
    {
        var language = await _db.Languages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == request.LanguageCode, cancellationToken);

        if (language is null)
        {
            return BadRequest(new { error = "Unknown language code." });
        }

        var match = new MatchHistory
        {
            UserAId = request.UserAId,
            UserBId = request.UserBId,
            GuestSessionAId = request.GuestSessionAId,
            GuestSessionBId = request.GuestSessionBId,
            PracticeLanguageId = language.Id,
            RoomId = request.RoomId,
            Status = MatchStatus.Matched,
            MatchedAtUtc = DateTime.UtcNow
        };

        await _matches.AddAsync(match, cancellationToken);
        return CreatedAtAction(nameof(GetByRoom), new { roomId = match.RoomId }, match);
    }

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetByRoom(string roomId, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByRoomIdAsync(roomId, cancellationToken);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPost("{roomId}/complete")]
    public async Task<IActionResult> Complete(string roomId, [FromBody] CompleteMatchRequest request, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByRoomIdAsync(roomId, cancellationToken);
        if (match is null)
        {
            return NotFound();
        }

        match.Status = MatchStatus.Completed;
        match.EndedAtUtc = DateTime.UtcNow;
        match.DurationSeconds = request.DurationSeconds;
        await _matches.UpdateAsync(match, cancellationToken);
        return NoContent();
    }
}

public record CreateMatchRequest(
    string RoomId,
    string LanguageCode,
    string? UserAId,
    string? UserBId,
    Guid? GuestSessionAId,
    Guid? GuestSessionBId);

public record CompleteMatchRequest(int DurationSeconds);
