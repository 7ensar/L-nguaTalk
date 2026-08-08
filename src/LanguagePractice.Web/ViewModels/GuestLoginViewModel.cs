using System.ComponentModel.DataAnnotations;

namespace LanguagePractice.Web.ViewModels;

public class GuestLoginViewModel
{
    [Required, StringLength(80, MinimumLength = 2)]
    [Display(Name = "Görünen ad")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Pratik dili")]
    public string PreferredLanguageCode { get; set; } = "en";
}
