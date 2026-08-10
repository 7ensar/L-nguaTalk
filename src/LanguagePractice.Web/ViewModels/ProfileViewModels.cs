using System.ComponentModel.DataAnnotations;
using LanguagePractice.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LanguagePractice.Web.ViewModels;

public sealed class ProfileDetailsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? NativeLanguageCode { get; set; }
    public string? NativeLanguageName { get; set; }
    public string? TargetLanguageCode { get; set; }
    public string? TargetLanguageName { get; set; }
    public LanguageLevel? LanguageLevel { get; set; }
    public Gender? Gender { get; set; }
    public IReadOnlyList<string> Interests { get; set; } = Array.Empty<string>();
    public bool IsOwnProfile { get; set; }
    public DateTime MemberSinceUtc { get; set; }
    public ProfileStatsViewModel Stats { get; set; } = new();
}

public sealed class ProfileStatsViewModel
{
    public int TotalMatches { get; set; }
    public int CompletedCalls { get; set; }
    public int UniquePartners { get; set; }
    public string FormattedTalkTime { get; set; } = "0m 00s";
    public int TotalTalkSeconds { get; set; }
}

public sealed class ProfileEditViewModel
{
    [Required, StringLength(64, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Bio { get; set; }

    [StringLength(10)]
    public string? NativeLanguageCode { get; set; }

    [StringLength(10)]
    public string? TargetLanguageCode { get; set; }

    public LanguageLevel? LanguageLevel { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>Virgülle ayrılmış ilgi alanları.</summary>
    [StringLength(500)]
    public string? InterestsRaw { get; set; }

    public bool IsDiscoverable { get; set; } = true;

    public Gender? PreferredPartnerGender { get; set; }
    public bool PreferSimilarLevel { get; set; } = true;
    public bool PreferSharedInterests { get; set; } = true;
    public bool BrowserNotificationsEnabled { get; set; } = true;

    public string? CurrentAvatarUrl { get; set; }

    [DataType(DataType.Upload)]
    public IFormFile? AvatarFile { get; set; }

    public bool RemoveAvatar { get; set; }

    public IEnumerable<SelectListItem> LanguageOptions { get; set; } = Array.Empty<SelectListItem>();
}
