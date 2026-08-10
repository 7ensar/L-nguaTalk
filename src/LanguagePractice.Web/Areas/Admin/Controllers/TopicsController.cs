using System.ComponentModel.DataAnnotations;
using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class TopicsController : Controller
{
    private readonly ApplicationDbContext _db;

    public TopicsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var topics = await _db.ConversationTopics
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return View(topics);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConversationTopicFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "İngilizce ve Türkçe metin gerekli.";
            return RedirectToAction(nameof(Index));
        }

        var maxSort = await _db.ConversationTopics.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
        _db.ConversationTopics.Add(new ConversationTopic
        {
            LanguageCode = NormalizeLang(model.LanguageCode),
            TextEn = model.TextEn.Trim(),
            TextTr = model.TextTr.Trim(),
            IsActive = true,
            SortOrder = maxSort + 1
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Konu eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, ConversationTopicFormModel model, CancellationToken cancellationToken)
    {
        var topic = await _db.ConversationTopics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (topic is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Güncelleme için İngilizce ve Türkçe metin gerekli.";
            return RedirectToAction(nameof(Index));
        }

        topic.LanguageCode = NormalizeLang(model.LanguageCode);
        topic.TextEn = model.TextEn.Trim();
        topic.TextTr = model.TextTr.Trim();
        topic.SortOrder = model.SortOrder;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Konu güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        var topic = await _db.ConversationTopics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (topic is null)
        {
            return NotFound();
        }

        topic.IsActive = !topic.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = topic.IsActive ? "Konu aktifleştirildi." : "Konu pasife alındı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var topic = await _db.ConversationTopics.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (topic is null)
        {
            return NotFound();
        }

        _db.ConversationTopics.Remove(topic);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Konu silindi.";
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizeLang(string? code)
    {
        var value = (code ?? "*").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "*";
        }

        return value.Length > 10 ? value[..10] : value;
    }
}

public class ConversationTopicFormModel
{
    [StringLength(10)]
    public string LanguageCode { get; set; } = "*";

    [Required, StringLength(300, MinimumLength = 3)]
    public string TextEn { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 3)]
    public string TextTr { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
