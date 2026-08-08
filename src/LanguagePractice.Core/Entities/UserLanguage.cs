using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

/// <summary>
/// Kullanıcının bildiği / pratik yapmak istediği dil ilişkisi.
/// </summary>
public class UserLanguage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int LanguageId { get; set; }
    public ProficiencyLevel Level { get; set; } = ProficiencyLevel.Beginner;
    public bool IsLearning { get; set; } = true;
    public bool IsTeaching { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
