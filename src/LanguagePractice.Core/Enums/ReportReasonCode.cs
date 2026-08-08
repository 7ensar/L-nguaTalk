namespace LanguagePractice.Core.Enums;

public static class ReportReasonCode
{
    public const string Inappropriate = "inappropriate";
    public const string Harassment = "harassment";
    public const string Spam = "spam";
    public const string Underage = "underage";
    public const string Other = "other";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Inappropriate] = "Uygunsuz davranış",
        [Harassment] = "Taciz / tehdit",
        [Spam] = "Spam / reklam",
        [Underage] = "Yaş ihlali",
        [Other] = "Diğer"
    };

    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code) && Labels.ContainsKey(code.Trim().ToLowerInvariant());
}
