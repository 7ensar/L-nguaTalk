using System.ComponentModel.DataAnnotations;

namespace LanguagePractice.Web.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(80, MinimumLength = 2)]
    [Display(Name = "Görünen ad")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
