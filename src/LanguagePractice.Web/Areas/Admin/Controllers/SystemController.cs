using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LanguagePractice.Infrastructure.Services;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class SystemController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ISignalingStatsClient _signaling;
    private readonly SignalingOptions _signalingOptions;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public SystemController(
        ApplicationDbContext db,
        ISignalingStatsClient signaling,
        IOptions<SignalingOptions> signalingOptions,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _db = db;
        _signaling = signaling;
        _signalingOptions = signalingOptions.Value;
        _configuration = configuration;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var signaling = await _signaling.GetStatsAsync(cancellationToken);
        var provider = _configuration["Database:Provider"] ?? "Sqlite";

        ViewBag.Environment = _env.EnvironmentName;
        ViewBag.DatabaseProvider = provider;
        ViewBag.SignalingUrl = _signalingOptions.PublicUrl;
        ViewBag.Languages = await _db.Languages.AsNoTracking().CountAsync(cancellationToken);
        ViewBag.GuestSessions = await _db.GuestSessions.AsNoTracking().CountAsync(cancellationToken);
        ViewBag.Signaling = signaling;

        return View();
    }
}
