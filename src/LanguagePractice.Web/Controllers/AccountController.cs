using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Localization;
using LanguagePractice.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LanguagePractice.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        IModerationService moderation,
        IStringLocalizer<SharedResources> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _moderation = moderation;
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        _db.Profiles.Add(new UserProfile { UserId = user.Id });
        await _db.SaveChangesAsync();

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, _localizer["Login_Invalid"]);
            return View(model);
        }

        if (await _moderation.IsUserBannedAsync(user.Id))
        {
            user = await _userManager.FindByIdAsync(user.Id) ?? user;
            ModelState.AddModelError(
                string.Empty,
                _localizer["Login_Banned", user.BanReason ?? "—"]);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, _localizer["Login_Invalid"]);
            return View(model);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin)
            && string.IsNullOrWhiteSpace(returnUrl))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        return LocalRedirect(returnUrl ?? Url.Action("Index", "Home")!);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
