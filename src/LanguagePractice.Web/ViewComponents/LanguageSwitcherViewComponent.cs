using LanguagePractice.Web.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LanguagePractice.Web.ViewComponents;

public class LanguageSwitcherViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        var current = AppCultures.Normalize(feature?.RequestCulture.UICulture.Name);
        var model = new LanguageSwitcherModel(
            current,
            AppCultures.All.First(x => x.Code == current),
            AppCultures.All);

        return View(model);
    }
}

public sealed record LanguageSwitcherModel(
    string CurrentCode,
    AppCulture Current,
    IReadOnlyList<AppCulture> Cultures);
