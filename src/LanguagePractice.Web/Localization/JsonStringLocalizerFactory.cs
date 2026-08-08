using System.Globalization;
using Microsoft.Extensions.Localization;

namespace LanguagePractice.Web.Localization;

public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly string _resourcesPath;

    public JsonStringLocalizerFactory(IWebHostEnvironment env)
    {
        _resourcesPath = Path.Combine(env.ContentRootPath, "Resources", "i18n");
    }

    public IStringLocalizer Create(Type resourceSource)
        => new JsonStringLocalizer(_resourcesPath, CultureInfo.CurrentUICulture);

    public IStringLocalizer Create(string baseName, string location)
        => new JsonStringLocalizer(_resourcesPath, CultureInfo.CurrentUICulture);
}
