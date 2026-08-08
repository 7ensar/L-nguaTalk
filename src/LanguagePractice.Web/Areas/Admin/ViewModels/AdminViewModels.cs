using System.ComponentModel.DataAnnotations;
using LanguagePractice.Core.Enums;

namespace LanguagePractice.Web.Areas.Admin.ViewModels;

public class AdminUserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public bool IsActive { get; set; }
}

public class AdminUserDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime? BannedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? Bio { get; set; }
    public string? NativeLanguageCode { get; set; }
    public string? TargetLanguageCode { get; set; }
    public string? LanguageLevel { get; set; }
    public IReadOnlyList<string> Interests { get; set; } = Array.Empty<string>();
    public int ReportCount { get; set; }
    public int OpenReportCount { get; set; }
    public List<AdminUserReportItemViewModel> Reports { get; set; } = new();
}

public class AdminUserReportItemViewModel
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Details { get; set; }
    public ReportStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string ReporterDisplayName { get; set; } = "Anonim";
    public string? RoomId { get; set; }
    public string? AutoAction { get; set; }
    public string? AdminNotes { get; set; }
}

public class BanUserViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(500, MinimumLength = 3)]
    [Display(Name = "Ban nedeni")]
    public string Reason { get; set; } = string.Empty;
}

public class AdminReportListItemViewModel
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Details { get; set; }
    public ReportStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ReportedUserId { get; set; }
    public Guid? ReportedGuestSessionId { get; set; }
    public string ReportedDisplayName { get; set; } = string.Empty;
    public string ReportedEmail { get; set; } = string.Empty;
    public string ReporterDisplayName { get; set; } = string.Empty;
    public string? RoomId { get; set; }
    public string? AutoAction { get; set; }
}

public class AdminBanListItemViewModel
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public BanType BanType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsSystemGenerated { get; set; }
}

public class AdminModerationPageViewModel
{
    public List<AdminReportListItemViewModel> Reports { get; set; } = new();
    public List<AdminBanListItemViewModel> Bans { get; set; } = new();
    public int OpenReportCount { get; set; }
    public int ActiveBanCount { get; set; }
}

public class ResolveReportViewModel
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    public ReportStatus Status { get; set; } = ReportStatus.Resolved;

    [StringLength(2000)]
    [Display(Name = "Admin notu")]
    public string? AdminNotes { get; set; }

    public bool AlsoBanUser { get; set; }

    [StringLength(500)]
    [Display(Name = "Ban nedeni")]
    public string? BanReason { get; set; }
}
