using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Helpers;
using LanguagePractice.Web.Localization;
using LanguagePractice.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace LanguagePractice.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProfileService _profiles;
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IProfileService profiles,
        ApplicationDbContext db,
        IStringLocalizer<SharedResources> localizer)
    {
        _userManager = userManager;
        _profiles = profiles;
        _db = db;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(string? id, CancellationToken cancellationToken)
    {
        var viewer = await _userManager.GetUserAsync(User);
        var targetId = string.IsNullOrWhiteSpace(id) ? viewer?.Id : id;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
        }

        await _profiles.EnsureProfileAsync(targetId, cancellationToken);
        var user = await _profiles.GetUserWithProfileAsync(targetId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return NotFound();
        }

        var isOwn = viewer is not null && viewer.Id == user.Id;
        if (!isOwn && user.Profile?.IsDiscoverable == false && !User.IsInRole(AppRoles.Admin))
        {
            return NotFound();
        }

        var langs = await _db.Languages.AsNoTracking()
            .Where(l => l.IsActive)
            .ToDictionaryAsync(l => l.Code, l => l.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var stats = await _profiles.GetStatsAsync(user.Id, cancellationToken);
        var profile = user.Profile;

        var vm = new ProfileDetailsViewModel
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = isOwn ? user.Email : null,
            Bio = profile?.Bio,
            AvatarUrl = profile?.AvatarUrl,
            NativeLanguageCode = profile?.NativeLanguageCode,
            NativeLanguageName = ResolveLangName(langs, profile?.NativeLanguageCode),
            TargetLanguageCode = profile?.TargetLanguageCode,
            TargetLanguageName = ResolveLangName(langs, profile?.TargetLanguageCode),
            LanguageLevel = profile?.LanguageLevel,
            Gender = profile?.Gender,
            Interests = ParseInterests(profile?.Interests),
            IsOwnProfile = isOwn,
            MemberSinceUtc = user.CreatedAtUtc,
            Stats = new ProfileStatsViewModel
            {
                TotalMatches = stats.TotalMatches,
                CompletedCalls = stats.CompletedCalls,
                UniquePartners = stats.UniquePartners,
                FormattedTalkTime = stats.FormattedTalkTime,
                TotalTalkSeconds = stats.TotalTalkSeconds
            }
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await _profiles.EnsureProfileAsync(user.Id, cancellationToken);
        user = await _profiles.GetUserWithProfileAsync(user.Id, cancellationToken);
        if (user is null)
        {
            return Challenge();
        }

        var profile = user.Profile!;
        var vm = new ProfileEditViewModel
        {
            DisplayName = user.DisplayName,
            Bio = profile.Bio,
            NativeLanguageCode = profile.NativeLanguageCode,
            TargetLanguageCode = profile.TargetLanguageCode,
            LanguageLevel = profile.LanguageLevel,
            Gender = profile.Gender,
            InterestsRaw = profile.Interests,
            IsDiscoverable = profile.IsDiscoverable,
            CurrentAvatarUrl = profile.AvatarUrl,
            LanguageOptions = await BuildLanguageOptionsAsync(cancellationToken)
        };

        ViewBag.AvailableInterests = await LoadActiveInterestTagsAsync(cancellationToken);
        ViewBag.Levels = Enum.GetValues<LanguageLevel>();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        model.LanguageOptions = await BuildLanguageOptionsAsync(cancellationToken);
        var available = await LoadActiveInterestTagsAsync(cancellationToken);
        ViewBag.AvailableInterests = available;
        ViewBag.Levels = Enum.GetValues<LanguageLevel>();

        if (!ModelState.IsValid)
        {
            model.CurrentAvatarUrl = (await _profiles.GetUserWithProfileAsync(user.Id, cancellationToken))?.Profile?.AvatarUrl;
            return View(model);
        }

        try
        {
            if (model.RemoveAvatar)
            {
                await _profiles.RemoveAvatarAsync(user.Id, cancellationToken);
            }
            else if (model.AvatarFile is { Length: > 0 })
            {
                await _profiles.SaveAvatarAsync(user.Id, model.AvatarFile, cancellationToken);
            }

            var allowed = available.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selected = ParseInterests(model.InterestsRaw)
                .Where(x => allowed.Contains(x))
                .Take(12)
                .ToList();

            await _profiles.UpdateProfileAsync(user.Id, new ProfileUpdateInput
            {
                DisplayName = model.DisplayName,
                Bio = model.Bio,
                NativeLanguageCode = model.NativeLanguageCode,
                TargetLanguageCode = model.TargetLanguageCode,
                LanguageLevel = model.LanguageLevel,
                Gender = model.Gender,
                Interests = selected,
                IsDiscoverable = model.IsDiscoverable
            }, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CurrentAvatarUrl = (await _profiles.GetUserWithProfileAsync(user.Id, cancellationToken))?.Profile?.AvatarUrl;
            return View(model);
        }

        TempData["Success"] = _localizer["Profile_Saved"].Value;
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    private async Task<IEnumerable<SelectListItem>> BuildLanguageOptionsAsync(CancellationToken cancellationToken)
    {
        var languages = await _db.Languages.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        return languages.Select(l => new SelectListItem
        {
            Value = l.Code,
            Text = $"{LanguageDisplay.Flag(l.Code)} {l.Name} ({l.NativeName})"
        });
    }

    private Task<List<InterestTag>> LoadActiveInterestTagsAsync(CancellationToken cancellationToken)
    {
        return _db.InterestTags.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private static string? ResolveLangName(IReadOnlyDictionary<string, string> langs, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return langs.TryGetValue(code, out var name) ? name : code.ToUpperInvariant();
    }

    private static IReadOnlyList<string> ParseInterests(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }
}
