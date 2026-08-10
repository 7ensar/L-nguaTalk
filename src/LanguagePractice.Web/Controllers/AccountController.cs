using System.Security.Claims;
using System.Text.Encodings.Web;
using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using LanguagePractice.Infrastructure.Services;
using LanguagePractice.Web.Localization;
using LanguagePractice.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IAppEmailSender _email;
    private readonly AuthExternalOptions _authExternal;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        IModerationService moderation,
        IStringLocalizer<SharedResources> localizer,
        IAppEmailSender email,
        IOptions<AuthExternalOptions> authExternal)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _moderation = moderation;
        _localizer = localizer;
        _email = email;
        _authExternal = authExternal.Value;
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

        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = confirmToken }, Request.Scheme)!;
        await _email.SendEmailAsync(
            user.Email!,
            "Confirm your LinguaTalk email",
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(confirmUrl)}'>clicking here</a>.");

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Info"] = _localizer["Account_ConfirmEmailSent"].Value;
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.GoogleEnabled = _authExternal.Google.Enabled;
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

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var url = Url.Action("ResetPassword", "Account", new { userId = user.Id, token }, Request.Scheme)!;
            await _email.SendEmailAsync(
                user.Email!,
                "Reset your LinguaTalk password",
                $"Reset your password by <a href='{HtmlEncoder.Default.Encode(url)}'>clicking here</a>.");
        }

        TempData["Info"] = _localizer["Account_ResetEmailSent"].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResetPassword(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["Info"] = _localizer["Account_PasswordResetOk"].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        TempData["Info"] = result.Succeeded
            ? _localizer["Account_EmailConfirmed"].Value
            : _localizer["Account_EmailConfirmFailed"].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Action("Index", "Home");
        if (!string.IsNullOrEmpty(remoteError))
        {
            TempData["Error"] = remoteError;
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
        {
            return LocalRedirect(returnUrl!);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = _localizer["Account_ExternalNoEmail"].Value;
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0]
            };
            var create = await _userManager.CreateAsync(user);
            if (!create.Succeeded)
            {
                TempData["Error"] = create.Errors.FirstOrDefault()?.Description ?? "Could not create account.";
                return RedirectToAction(nameof(Login));
            }

            _db.Profiles.Add(new UserProfile { UserId = user.Id });
            await _db.SaveChangesAsync();
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl!);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
