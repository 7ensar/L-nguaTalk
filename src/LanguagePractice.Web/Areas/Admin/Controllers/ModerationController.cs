using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Enums;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LanguagePractice.Core.Entities;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class ModerationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;
    private readonly UserManager<ApplicationUser> _userManager;

    public ModerationController(
        ApplicationDbContext db,
        IModerationService moderation,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _moderation = moderation;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var reports = await _db.UserReports
            .AsNoTracking()
            .Include(x => x.ReportedUser)
            .Include(x => x.ReporterUser)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new AdminReportListItemViewModel
            {
                Id = x.Id,
                Reason = x.Reason,
                ReasonCode = x.ReasonCode,
                Details = x.Details,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                ReportedUserId = x.ReportedUserId,
                ReportedGuestSessionId = x.ReportedGuestSessionId,
                ReportedDisplayName = x.ReportedUser != null
                    ? x.ReportedUser.DisplayName
                    : (x.ReportedPeerDisplayName ?? "Misafir / bilinmeyen"),
                ReportedEmail = x.ReportedUser != null ? (x.ReportedUser.Email ?? "") : "",
                ReporterDisplayName = x.ReporterUser != null ? x.ReporterUser.DisplayName : "Misafir",
                RoomId = x.RoomId,
                AutoAction = x.AutoAction
            })
            .ToListAsync(cancellationToken);

        var bans = await _db.BanRecords
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.GuestSession)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new AdminBanListItemViewModel
            {
                Id = x.Id,
                UserId = x.UserId,
                GuestSessionId = x.GuestSessionId,
                DisplayName = x.User != null
                    ? x.User.DisplayName
                    : (x.GuestSession != null ? x.GuestSession.DisplayName : (x.PeerKey ?? "—")),
                Email = x.User != null ? (x.User.Email ?? "") : "",
                BanType = x.BanType,
                Reason = x.Reason,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                IsSystemGenerated = x.IsSystemGenerated
            })
            .ToListAsync(cancellationToken);

        return View(new AdminModerationPageViewModel
        {
            Reports = reports,
            Bans = bans,
            OpenReportCount = reports.Count(x => x.Status is ReportStatus.Open or ReportStatus.UnderReview),
            ActiveBanCount = bans.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickBan(Guid reportId, string banType, CancellationToken cancellationToken)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin is null)
        {
            return Challenge();
        }

        var report = await _db.UserReports.FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var type = string.Equals(banType, "temporary", StringComparison.OrdinalIgnoreCase)
            ? BanType.Temporary
            : BanType.Permanent;

        var reason = $"Admin ban ({report.Reason})";

        if (!string.IsNullOrWhiteSpace(report.ReportedUserId))
        {
            await _moderation.BanUserAsync(report.ReportedUserId, admin.Id, reason, type, reportId: report.Id, cancellationToken: cancellationToken);
        }
        else if (report.ReportedGuestSessionId.HasValue)
        {
            await _moderation.BanGuestAsync(report.ReportedGuestSessionId.Value, admin.Id, reason, type, reportId: report.Id, cancellationToken: cancellationToken);
        }
        else
        {
            TempData["Error"] = "Bu şikayette banlanacak kimlik yok (yalnızca socket).";
            return RedirectToAction(nameof(Index));
        }

        report.Status = ReportStatus.Resolved;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByAdminId = admin.Id;
        report.AdminNotes = $"Quick ban: {type}";
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = type == BanType.Temporary ? "Geçici ban uygulandı." : "Kalıcı ban uygulandı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(Guid reportId, CancellationToken cancellationToken)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin is null)
        {
            return Challenge();
        }

        await _moderation.ResolveReportAsync(reportId, admin.Id, ReportStatus.Dismissed, "Dismissed by admin", cancellationToken);
        TempData["Success"] = "Şikayet reddedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LiftBan(Guid banId, CancellationToken cancellationToken)
    {
        await _moderation.DeactivateBanAsync(banId, cancellationToken);
        TempData["Success"] = "Ban kaldırıldı. Kullanıcı tekrar girebilir.";
        return RedirectToAction(nameof(Index));
    }
}
