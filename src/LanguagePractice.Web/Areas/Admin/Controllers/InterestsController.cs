using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class InterestsController : Controller
{
    private readonly ApplicationDbContext _db;

    public InterestsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tags = await _db.InterestTags
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        return View(tags);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InterestTagFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Etiket adı gerekli.";
            return RedirectToAction(nameof(Index));
        }

        var slug = NormalizeSlug(model.DisplayName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            TempData["Error"] = "Geçersiz etiket.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _db.InterestTags.AnyAsync(x => x.Slug == slug, cancellationToken);
        if (exists)
        {
            TempData["Error"] = "Bu etiket zaten var.";
            return RedirectToAction(nameof(Index));
        }

        var maxSort = await _db.InterestTags.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
        _db.InterestTags.Add(new InterestTag
        {
            Slug = slug,
            DisplayName = model.DisplayName.Trim(),
            IsActive = true,
            SortOrder = maxSort + 1
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Etiket eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        var tag = await _db.InterestTags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tag is null)
        {
            return NotFound();
        }

        tag.IsActive = !tag.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = tag.IsActive ? "Etiket aktifleştirildi." : "Etiket pasife alındı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var tag = await _db.InterestTags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tag is null)
        {
            return NotFound();
        }

        _db.InterestTags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Etiket silindi.";
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s_-]+", "");
        slug = Regex.Replace(slug, @"[\s_]+", "-").Trim('-');
        return slug.Length > 32 ? slug[..32] : slug;
    }
}

public class InterestTagFormModel
{
    [Required, StringLength(64, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;
}
