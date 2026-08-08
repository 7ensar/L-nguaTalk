namespace LanguagePractice.Core.Entities;

/// <summary>
/// Admin tarafından yönetilen profil ilgi alanı etiketi.
/// </summary>
public class InterestTag
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
