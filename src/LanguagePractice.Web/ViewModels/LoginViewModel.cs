using System.ComponentModel.DataAnnotations;

namespace LanguagePractice.Web.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }
}
