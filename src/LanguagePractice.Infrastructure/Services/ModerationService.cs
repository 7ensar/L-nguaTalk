using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Infrastructure.Services;

public class ModerationOptions
{
    public const string SectionName = "Moderation";
    public int TempBanHours { get; set; } = 24;
    public int AutoTempBanReportThreshold { get; set; } = 3;
    public int AutoTempBanWindowHours { get; set; } = 24;
    public int AutoPermanentBanReportThreshold { get; set; } = 5;
    public string? SignalingModerationKey { get; set; } = "dev-moderation-key";
}

public record LiveReportResult(
    Guid ReportId,
    bool CallEnded,
    bool TempBanned,
    bool PermanentBanned,
    string Message);

public interface IModerationService
{
    Task BanUserAsync(string userId, string? adminId, string reason, BanType banType = BanType.Permanent, TimeSpan? duration = null, Guid? reportId = null, CancellationToken cancellationToken = default);
    Task BanGuestAsync(Guid guestSessionId, string? adminId, string reason, BanType banType = BanType.Temporary, TimeSpan? duration = null, Guid? reportId = null, CancellationToken cancellationToken = default);
    Task UnbanUserAsync(string userId, CancellationToken cancellationToken = default);
    Task UnbanGuestAsync(Guid guestSessionId, CancellationToken cancellationToken = default);
    Task DeactivateBanAsync(Guid banId, CancellationToken cancellationToken = default);
    Task<UserReport?> ResolveReportAsync(Guid reportId, string adminId, ReportStatus status, string? notes, CancellationToken cancellationToken = default);
    Task<LiveReportResult> SubmitLiveReportAsync(LiveReportInput input, CancellationToken cancellationToken = default);
    Task<bool> IsUserBannedAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsGuestBannedAsync(Guid guestSessionId, CancellationToken cancellationToken = default);
}

public record LiveReportInput(
    string? ReporterUserId,
    Guid? ReporterGuestSessionId,
    string? ReportedUserId,
    Guid? ReportedGuestSessionId,
    string? ReportedPeerSocketId,
    string? ReportedPeerDisplayName,
    string? RoomId,
    string ReasonCode,
    string? Details,
    string? ReporterIp);

public class ModerationService : IModerationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ModerationOptions _options;
    private readonly SignalingOptions _signaling;
    private readonly ILogger<ModerationService> _logger;

    public ModerationService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IHttpClientFactory httpClientFactory,
        IOptions<ModerationOptions> options,
        IOptions<SignalingOptions> signaling,
        ILogger<ModerationService> logger)
    {
        _db = db;
        _userManager = userManager;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _signaling = signaling.Value;
        _logger = logger;
    }

    public async Task BanUserAsync(
        string userId,
        string? adminId,
        string reason,
        BanType banType = BanType.Permanent,
        TimeSpan? duration = null,
        Guid? reportId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var expires = banType == BanType.Temporary
            ? DateTime.UtcNow.Add(duration ?? TimeSpan.FromHours(_options.TempBanHours))
            : (DateTime?)null;

        user.IsBanned = true;
        user.BannedAtUtc = DateTime.UtcNow;
        user.BanReason = reason.Trim();
        user.BannedByAdminId = adminId;
        user.IsActive = banType != BanType.Permanent;
        user.LockoutEnabled = true;
        user.LockoutEnd = expires.HasValue
            ? new DateTimeOffset(expires.Value)
            : DateTimeOffset.UtcNow.AddYears(100);

        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        _db.BanRecords.Add(new BanRecord
        {
            UserId = userId,
            BanType = banType,
            Reason = reason.Trim(),
            ExpiresAtUtc = expires,
            CreatedByAdminId = adminId,
            IsSystemGenerated = string.IsNullOrWhiteSpace(adminId),
            RelatedReportId = reportId,
            IsActive = true
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task BanGuestAsync(
        Guid guestSessionId,
        string? adminId,
        string reason,
        BanType banType = BanType.Temporary,
        TimeSpan? duration = null,
        Guid? reportId = null,
        CancellationToken cancellationToken = default)
    {
        var guest = await _db.GuestSessions.FirstOrDefaultAsync(x => x.Id == guestSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Misafir oturumu bulunamadı.");

        var expires = banType == BanType.Temporary
            ? DateTime.UtcNow.Add(duration ?? TimeSpan.FromHours(_options.TempBanHours))
            : DateTime.UtcNow.AddYears(100);

        guest.IsBanned = true;
        guest.IsActive = false;
        guest.BannedAtUtc = DateTime.UtcNow;
        guest.BanReason = reason.Trim();
        guest.BanExpiresAtUtc = expires;

        _db.BanRecords.Add(new BanRecord
        {
            GuestSessionId = guestSessionId,
            BanType = banType,
            Reason = reason.Trim(),
            ExpiresAtUtc = expires,
            CreatedByAdminId = adminId,
            IsSystemGenerated = string.IsNullOrWhiteSpace(adminId),
            RelatedReportId = reportId,
            IsActive = true
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnbanUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        user.IsBanned = false;
        user.BannedAtUtc = null;
        user.BanReason = null;
        user.BannedByAdminId = null;
        user.IsActive = true;
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);

        var bans = await _db.BanRecords
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var ban in bans)
        {
            ban.IsActive = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnbanGuestAsync(Guid guestSessionId, CancellationToken cancellationToken = default)
    {
        var guest = await _db.GuestSessions.FirstOrDefaultAsync(x => x.Id == guestSessionId, cancellationToken);
        if (guest is null)
        {
            return;
        }

        guest.IsBanned = false;
        guest.BanReason = null;
        guest.BannedAtUtc = null;
        guest.BanExpiresAtUtc = null;
        guest.IsActive = guest.ExpiresAtUtc > DateTime.UtcNow;

        var bans = await _db.BanRecords
            .Where(x => x.GuestSessionId == guestSessionId && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var ban in bans)
        {
            ban.IsActive = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateBanAsync(Guid banId, CancellationToken cancellationToken = default)
    {
        var ban = await _db.BanRecords.FirstOrDefaultAsync(x => x.Id == banId, cancellationToken);
        if (ban is null)
        {
            return;
        }

        ban.IsActive = false;
        if (!string.IsNullOrWhiteSpace(ban.UserId))
        {
            await UnbanUserAsync(ban.UserId, cancellationToken);
            return;
        }

        if (ban.GuestSessionId.HasValue)
        {
            await UnbanGuestAsync(ban.GuestSessionId.Value, cancellationToken);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserReport?> ResolveReportAsync(
        Guid reportId,
        string adminId,
        ReportStatus status,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var report = await _db.UserReports.FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        report.Status = status;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByAdminId = adminId;
        report.AdminNotes = notes;
        await _db.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<LiveReportResult> SubmitLiveReportAsync(LiveReportInput input, CancellationToken cancellationToken = default)
    {
        var code = (input.ReasonCode ?? ReportReasonCode.Other).Trim().ToLowerInvariant();
        if (!ReportReasonCode.IsValid(code))
        {
            code = ReportReasonCode.Other;
        }

        var reasonLabel = ReportReasonCode.Labels[code];
        var report = new UserReport
        {
            ReporterUserId = input.ReporterUserId,
            ReporterGuestSessionId = input.ReporterGuestSessionId,
            ReportedUserId = input.ReportedUserId,
            ReportedGuestSessionId = input.ReportedGuestSessionId,
            ReportedPeerSocketId = input.ReportedPeerSocketId,
            ReportedPeerDisplayName = input.ReportedPeerDisplayName,
            RoomId = input.RoomId,
            ReasonCode = code,
            Reason = reasonLabel,
            Details = input.Details?.Trim(),
            Status = ReportStatus.Open,
            ReporterIpHash = HashIp(input.ReporterIp),
            AutoAction = "call_ended"
        };

        _db.UserReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        // Anında görüşmeyi bitir
        await NotifySignalingForceDisconnectAsync(input.RoomId, input.ReportedPeerSocketId, cancellationToken);

        var tempBanned = false;
        var permanentBanned = false;
        var message = "Şikayet alındı. Görüşme sonlandırıldı.";

        // Otomatik ban eşikleri
        var since = DateTime.UtcNow.AddHours(-_options.AutoTempBanWindowHours);
        var recentCount = await _db.UserReports.CountAsync(x =>
                x.CreatedAtUtc >= since &&
                (
                    (!string.IsNullOrWhiteSpace(input.ReportedUserId) && x.ReportedUserId == input.ReportedUserId) ||
                    (input.ReportedGuestSessionId.HasValue && x.ReportedGuestSessionId == input.ReportedGuestSessionId)
                ),
            cancellationToken);

        // Taciz / yaş ihlali → anında geçici ban
        var severe = code == ReportReasonCode.Harassment || code == ReportReasonCode.Underage;
        if (severe || recentCount >= _options.AutoTempBanReportThreshold)
        {
            var banType = recentCount >= _options.AutoPermanentBanReportThreshold
                ? BanType.Permanent
                : BanType.Temporary;

            if (!string.IsNullOrWhiteSpace(input.ReportedUserId))
            {
                await BanUserAsync(
                    input.ReportedUserId,
                    adminId: null,
                    reason: $"Otomatik ban ({reasonLabel})",
                    banType,
                    duration: TimeSpan.FromHours(_options.TempBanHours),
                    reportId: report.Id,
                    cancellationToken);
            }
            else if (input.ReportedGuestSessionId.HasValue)
            {
                await BanGuestAsync(
                    input.ReportedGuestSessionId.Value,
                    adminId: null,
                    reason: $"Otomatik ban ({reasonLabel})",
                    banType,
                    duration: TimeSpan.FromHours(_options.TempBanHours),
                    reportId: report.Id,
                    cancellationToken);
            }

            tempBanned = banType == BanType.Temporary;
            permanentBanned = banType == BanType.Permanent;
            report.AutoAction = permanentBanned ? "permanent_ban" : "temporary_ban";
            report.Status = ReportStatus.UnderReview;
            await _db.SaveChangesAsync(cancellationToken);
            message = permanentBanned
                ? "Şikayet alındı. Kullanıcı kalıcı olarak engellendi."
                : "Şikayet alındı. Kullanıcı geçici olarak engellendi.";
        }

        return new LiveReportResult(report.Id, true, tempBanned, permanentBanned, message);
    }

    public async Task<bool> IsUserBannedAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        if (!user.IsBanned)
        {
            return false;
        }

        // Süresi dolmuş geçici banları temizle
        var activeTemp = await _db.BanRecords
            .Where(x => x.UserId == userId && x.IsActive && x.BanType == BanType.Temporary && x.ExpiresAtUtc != null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeTemp?.ExpiresAtUtc < DateTime.UtcNow)
        {
            await UnbanUserAsync(userId, cancellationToken);
            return false;
        }

        return true;
    }

    public async Task<bool> IsGuestBannedAsync(Guid guestSessionId, CancellationToken cancellationToken = default)
    {
        var guest = await _db.GuestSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == guestSessionId, cancellationToken);
        if (guest is null)
        {
            return false;
        }

        if (!guest.IsBanned)
        {
            return false;
        }

        if (guest.BanExpiresAtUtc.HasValue && guest.BanExpiresAtUtc < DateTime.UtcNow)
        {
            await UnbanGuestAsync(guestSessionId, cancellationToken);
            return false;
        }

        return true;
    }

    private async Task NotifySignalingForceDisconnectAsync(
        string? roomId,
        string? reportedSocketId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roomId) && string.IsNullOrWhiteSpace(reportedSocketId))
        {
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SignalingModeration");
            var baseUrl = _signaling.PublicUrl.TrimEnd('/') + "/";
            client.BaseAddress ??= new Uri(baseUrl);
            using var request = new HttpRequestMessage(HttpMethod.Post, "moderation/force-disconnect");
            request.Headers.Add("X-Moderation-Key", _options.SignalingModerationKey ?? "");
            request.Content = JsonContent.Create(new
            {
                roomId,
                reportedSocketId,
                reason = "user_report"
            });
            await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signaling force-disconnect çağrısı başarısız.");
        }
    }

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip.Trim()));
        return Convert.ToHexString(bytes)[..32];
    }
}
