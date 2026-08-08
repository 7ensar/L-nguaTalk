using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IModerationService _moderation;
    private readonly ApplicationDbContext _db;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        IModerationService moderation,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _moderation = moderation;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? filter, CancellationToken cancellationToken)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                x.DisplayName.ToLower().Contains(term));
        }

        filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLowerInvariant();
        query = filter switch
        {
            "banned" => query.Where(x => x.IsBanned),
            "active" => query.Where(x => !x.IsBanned && x.IsActive),
            _ => query
        };

        var users = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new AdminUserListItemViewModel
            {
                Id = x.Id,
                Email = x.Email ?? "",
                DisplayName = x.DisplayName,
                CreatedAtUtc = x.CreatedAtUtc,
                LastLoginAtUtc = x.LastLoginAtUtc,
                IsBanned = x.IsBanned,
                BanReason = x.BanReason,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        ViewBag.Query = q;
        ViewBag.Filter = filter;
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var reportQuery = _db.UserReports.AsNoTracking().Where(x => x.ReportedUserId == id);
        var reportCount = await reportQuery.CountAsync(cancellationToken);
        var openReportCount = await reportQuery.CountAsync(
            x => x.Status == ReportStatus.Open || x.Status == ReportStatus.UnderReview,
            cancellationToken);

        var reports = await reportQuery
            .Include(x => x.ReporterUser)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .Select(x => new AdminUserReportItemViewModel
            {
                Id = x.Id,
                Reason = x.Reason,
                ReasonCode = x.ReasonCode,
                Details = x.Details,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                ReporterDisplayName = x.ReporterUser != null ? x.ReporterUser.DisplayName : "Anonim",
                RoomId = x.RoomId,
                AutoAction = x.AutoAction,
                AdminNotes = x.AdminNotes
            })
            .ToListAsync(cancellationToken);

        var interests = string.IsNullOrWhiteSpace(user.Profile?.Interests)
            ? Array.Empty<string>()
            : user.Profile!.Interests!
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var model = new AdminUserDetailsViewModel
        {
            Id = user.Id,
            Email = user.Email ?? "",
            DisplayName = user.DisplayName,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc,
            IsBanned = user.IsBanned,
            BanReason = user.BanReason,
            BannedAtUtc = user.BannedAtUtc,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            Bio = user.Profile?.Bio,
            NativeLanguageCode = user.Profile?.NativeLanguageCode,
            TargetLanguageCode = user.Profile?.TargetLanguageCode,
            LanguageLevel = user.Profile?.LanguageLevel?.ToString(),
            Interests = interests,
            ReportCount = reportCount,
            OpenReportCount = openReportCount,
            Reports = reports
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(BanUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Ban nedeni gerekli.";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }

        var admin = await _userManager.GetUserAsync(User);
        if (admin is null)
        {
            return Challenge();
        }

        if (model.UserId == admin.Id)
        {
            TempData["Error"] = "Kendinizi banlayamazsınız.";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }

        await _moderation.BanUserAsync(model.UserId, admin.Id, model.Reason, BanType.Permanent, cancellationToken: cancellationToken);
        TempData["Success"] = "Kullanıcı banlandı.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(string userId, CancellationToken cancellationToken)
    {
        await _moderation.UnbanUserAsync(userId, cancellationToken);
        TempData["Success"] = "Ban kaldırıldı.";
        return RedirectToAction(nameof(Details), new { id = userId });
    }
}
