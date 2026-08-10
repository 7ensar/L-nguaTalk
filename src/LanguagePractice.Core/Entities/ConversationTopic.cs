namespace LanguagePractice.Core.Entities;

public class ConversationTopic
{
    public int Id { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string TextEn { get; set; } = string.Empty;
    public string TextTr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
