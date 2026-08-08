namespace LanguagePractice.Core.Entities;

/// <summary>
/// Desteklenen diller (ISO 639-1 kodu ile).
/// </summary>
public class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<UserLanguage> UserLanguages { get; set; } = new List<UserLanguage>();
}
