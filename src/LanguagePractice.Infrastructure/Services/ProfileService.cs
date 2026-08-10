using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace LanguagePractice.Infrastructure.Services;

public sealed class ProfileStats
{
    public int TotalMatches { get; init; }
    public int CompletedCalls { get; init; }
    public int TotalTalkSeconds { get; init; }
    public int UniquePartners { get; init; }

    public string FormattedTalkTime
    {
        get
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, TotalTalkSeconds));
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            }

            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
    }
}

public sealed class ProfileUpdateInput
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? NativeLanguageCode { get; set; }
    public string? TargetLanguageCode { get; set; }
    public LanguageLevel? LanguageLevel { get; set; }
    public Gender? Gender { get; set; }
    public IReadOnlyList<string> Interests { get; set; } = Array.Empty<string>();
    public bool IsDiscoverable { get; set; } = true;
    public Gender? PreferredPartnerGender { get; set; }
    public bool PreferSimilarLevel { get; set; } = true;
    public bool PreferSharedInterests { get; set; } = true;
    public bool BrowserNotificationsEnabled { get; set; } = true;
}

public interface IProfileService
{
    Task<ApplicationUser?> GetUserWithProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task EnsureProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<ProfileStats> GetStatsAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(string userId, ProfileUpdateInput input, CancellationToken cancellationToken = default);
    Task<string> SaveAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default);
    Task RemoveAvatarAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class ProfileService : IProfileService
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private const long MaxAvatarBytes = 2 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHostEnvironment _env;

    public ProfileService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public Task<ApplicationUser?> GetUserWithProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task EnsureProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Profiles.AnyAsync(p => p.UserId == userId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.Profiles.Add(new UserProfile { UserId = userId });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfileStats> GetStatsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var matches = await _db.MatchHistories
            .AsNoTracking()
            .Where(m => m.UserAId == userId || m.UserBId == userId)
            .Select(m => new
            {
                m.Status,
                m.DurationSeconds,
                m.UserAId,
                m.UserBId,
                m.GuestSessionAId,
                m.GuestSessionBId
            })
            .ToListAsync(cancellationToken);

        var completed = matches.Where(m => m.Status == MatchStatus.Completed).ToList();
        var talkSeconds = completed.Sum(m => m.DurationSeconds ?? 0);

        var partners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in matches)
        {
            if (m.UserAId == userId)
            {
                if (!string.IsNullOrWhiteSpace(m.UserBId))
                {
                    partners.Add("u:" + m.UserBId);
                }
                else if (m.GuestSessionBId.HasValue)
                {
                    partners.Add("g:" + m.GuestSessionBId);
                }
            }
            else if (m.UserBId == userId)
            {
                if (!string.IsNullOrWhiteSpace(m.UserAId))
                {
                    partners.Add("u:" + m.UserAId);
                }
                else if (m.GuestSessionAId.HasValue)
                {
                    partners.Add("g:" + m.GuestSessionAId);
                }
            }
        }

        return new ProfileStats
        {
            TotalMatches = matches.Count,
            CompletedCalls = completed.Count,
            TotalTalkSeconds = talkSeconds,
            UniquePartners = partners.Count
        };
    }

    public async Task UpdateProfileAsync(string userId, ProfileUpdateInput input, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var displayName = (input.DisplayName ?? string.Empty).Trim();
        if (displayName.Length is < 2 or > 64)
        {
            throw new ArgumentException("Display name must be 2–64 characters.");
        }

        user.DisplayName = displayName;
        await _userManager.UpdateAsync(user);

        await EnsureProfileAsync(userId, cancellationToken);
        var profile = await _db.Profiles.FirstAsync(p => p.UserId == userId, cancellationToken);

        profile.Bio = string.IsNullOrWhiteSpace(input.Bio) ? null : input.Bio.Trim();
        if (profile.Bio is { Length: > 1000 })
        {
            profile.Bio = profile.Bio[..1000];
        }

        profile.NativeLanguageCode = NormalizeLang(input.NativeLanguageCode);
        profile.TargetLanguageCode = NormalizeLang(input.TargetLanguageCode);
        profile.LanguageLevel = input.LanguageLevel;
        profile.Gender = input.Gender;
        profile.Interests = SerializeInterests(input.Interests);
        profile.IsDiscoverable = input.IsDiscoverable;
        profile.PreferredPartnerGender = input.PreferredPartnerGender;
        profile.PreferSimilarLevel = input.PreferSimilarLevel;
        profile.PreferSharedInterests = input.PreferSharedInterests;
        profile.BrowserNotificationsEnabled = input.BrowserNotificationsEnabled;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> SaveAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("Avatar file is required.");
        }

        if (file.Length > MaxAvatarBytes)
        {
            throw new ArgumentException("Avatar must be 2 MB or smaller.");
        }

        var contentType = file.ContentType ?? "";
        if (!AllowedImageTypes.Contains(contentType))
        {
            throw new ArgumentException("Only JPG, PNG, WEBP or GIF images are allowed.");
        }

        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        await EnsureProfileAsync(userId, cancellationToken);
        var profile = await _db.Profiles.FirstAsync(p => p.UserId == userId, cancellationToken);

        var webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
        var avatarDir = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(avatarDir);

        // Eski dosyayı temizle
        DeleteAvatarFile(webRoot, profile.AvatarUrl);

        var fileName = $"{userId}{ext}";
        var physicalPath = Path.Combine(avatarDir, fileName);
        await using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = $"/uploads/avatars/{fileName}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        profile.AvatarUrl = $"/uploads/avatars/{fileName}";
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return url;
    }

    public async Task RemoveAvatarAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            return;
        }

        var webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
        DeleteAvatarFile(webRoot, profile.AvatarUrl);
        profile.AvatarUrl = null;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeLang(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToLowerInvariant();
    }

    private static string? SerializeInterests(IReadOnlyList<string> interests)
    {
        if (interests is null || interests.Count == 0)
        {
            return null;
        }

        var cleaned = interests
            .Select(x => (x ?? string.Empty).Trim().ToLowerInvariant())
            .Where(x => x.Length is >= 2 and <= 32)
            .Select(x => new string(x.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim())
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return cleaned.Count == 0 ? null : string.Join(',', cleaned);
    }

    private static void DeleteAvatarFile(string webRoot, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        var relative = avatarUrl.Split('?', 2)[0].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (!relative.StartsWith("uploads" + Path.DirectorySeparatorChar + "avatars", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var full = Path.Combine(webRoot, relative);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }
}
