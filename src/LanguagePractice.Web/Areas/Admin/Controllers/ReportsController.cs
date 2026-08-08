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
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(
        ApplicationDbContext db,
        IModerationService moderation,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _moderation = moderation;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Moderation");

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var report = await _db.UserReports
            .Include(x => x.ReportedUser)
            .Include(x => x.ReporterUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return report is null ? NotFound() : View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(ResolveReportViewModel model, CancellationToken cancellationToken)
    {
        var admin = await _userManager.GetUserAsync(User);
        if (admin is null)
        {
            return Challenge();
        }

        await _moderation.ResolveReportAsync(
            model.ReportId,
            admin.Id,
            model.Status,
            model.AdminNotes,
            cancellationToken);

        if (model.AlsoBanUser)
        {
            var report = await _db.UserReports.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.ReportId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(report?.ReportedUserId))
            {
                await _moderation.BanUserAsync(
                    report.ReportedUserId,
                    admin.Id,
                    model.BanReason ?? "Admin kararı",
                    BanType.Permanent,
                    reportId: report.Id,
                    cancellationToken: cancellationToken);
            }
        }

        TempData["Success"] = "Rapor güncellendi.";
        return RedirectToAction(nameof(Details), new { id = model.ReportId });
    }
}
