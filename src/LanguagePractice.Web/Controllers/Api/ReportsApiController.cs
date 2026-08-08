using System.ComponentModel.DataAnnotations;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Controllers.Api;

[ApiController]
[Route("api/reports")]
[EnableRateLimiting("reports")]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IModerationService _moderation;
    private readonly IGuestSessionService _guestSessions;

    public ReportsApiController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IModerationService moderation,
        IGuestSessionService guestSessions)
    {
        _db = db;
        _userManager = userManager;
        _moderation = moderation;
        _guestSessions = guestSessions;
    }

    [HttpPost("live")]
    public async Task<IActionResult> CreateLive([FromBody] CreateLiveReportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.ReportedPeerSocketId) &&
            string.IsNullOrWhiteSpace(request.ReportedUserId) &&
            !request.ReportedGuestSessionId.HasValue)
        {
            return BadRequest(new { error = "Reported peer identity is required." });
        }

        string? reporterUserId = null;
        Guid? reporterGuestId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null || await _moderation.IsUserBannedAsync(user.Id, cancellationToken))
            {
                return Unauthorized(new { error = "Banned or invalid user." });
            }

            reporterUserId = user.Id;
        }
        else
        {
            var token = Request.Cookies[GuestController.GuestTokenCookie];
            var guest = string.IsNullOrWhiteSpace(token)
                ? null
                : await _guestSessions.ValidateAsync(token, cancellationToken);
            if (guest is null)
            {
                return Unauthorized(new { error = "Guest session required." });
            }

            reporterGuestId = guest.Id;
        }

        var since = DateTime.UtcNow.AddSeconds(-60);
        var duplicate = await _db.UserReports.AnyAsync(x =>
                x.CreatedAtUtc >= since &&
                x.RoomId == request.RoomId &&
                (
                    (reporterUserId != null && x.ReporterUserId == reporterUserId) ||
                    (reporterGuestId != null && x.ReporterGuestSessionId == reporterGuestId)
                ),
            cancellationToken);

        if (duplicate)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Please wait before sending another report." });
        }

        var result = await _moderation.SubmitLiveReportAsync(
            new LiveReportInput(
                ReporterUserId: reporterUserId,
                ReporterGuestSessionId: reporterGuestId,
                ReportedUserId: request.ReportedUserId,
                ReportedGuestSessionId: request.ReportedGuestSessionId,
                ReportedPeerSocketId: request.ReportedPeerSocketId,
                ReportedPeerDisplayName: request.ReportedPeerDisplayName,
                RoomId: request.RoomId,
                ReasonCode: request.ReasonCode,
                Details: request.Details,
                ReporterIp: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(new
        {
            reportId = result.ReportId,
            callEnded = result.CallEnded,
            tempBanned = result.TempBanned,
            permanentBanned = result.PermanentBanned,
            message = result.Message
        });
    }
}

public class CreateLiveReportRequest
{
    [Required, StringLength(40)]
    public string ReasonCode { get; set; } = ReportReasonCode.Inappropriate;

    [StringLength(2000)]
    public string? Details { get; set; }

    [StringLength(64)]
    public string? RoomId { get; set; }

    [StringLength(128)]
    public string? ReportedPeerSocketId { get; set; }

    [StringLength(80)]
    public string? ReportedPeerDisplayName { get; set; }

    public string? ReportedUserId { get; set; }
    public Guid? ReportedGuestSessionId { get; set; }
}
