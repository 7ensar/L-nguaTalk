using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class DashboardController : Controller
{
    private readonly IAdminDashboardService _dashboard;

    public DashboardController(IAdminDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var stats = await _dashboard.GetStatsAsync(cancellationToken);
        return View(stats);
    }
}
