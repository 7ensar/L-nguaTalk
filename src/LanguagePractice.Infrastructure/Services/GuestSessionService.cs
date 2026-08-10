using System.Security.Cryptography;
using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Infrastructure.Services;

public class GuestSessionService : IGuestSessionService
{
    private readonly ApplicationDbContext _db;

    public GuestSessionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<GuestSession> CreateAsync(
        string displayName,
        string? preferredLanguageCode,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var session = new GuestSession
        {
            DisplayName = displayName.Trim(),
            PreferredLanguageCode = preferredLanguageCode,
            // Cookie'de + / karakterleri bozulmasın diye URL-safe Base64
            SessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_'),
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
        };

        _db.GuestSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<GuestSession?> ValidateAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        var session = await _db.GuestSessions.FirstOrDefaultAsync(
            x => x.SessionToken == sessionToken && x.ExpiresAtUtc > DateTime.UtcNow,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.IsBanned && session.BanExpiresAtUtc.HasValue && session.BanExpiresAtUtc <= DateTime.UtcNow)
        {
            session.IsBanned = false;
            session.BanReason = null;
            session.BannedAtUtc = null;
            session.BanExpiresAtUtc = null;
            session.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (!session.IsActive || session.IsBanned)
        {
            return null;
        }

        return session;
    }

    public async Task TouchAsync(Guid guestSessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.GuestSessions.FindAsync([guestSessionId], cancellationToken);
        if (session is null)
        {
            return;
        }

        session.LastSeenAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
