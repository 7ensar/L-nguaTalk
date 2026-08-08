using LanguagePractice.Core.Entities;

namespace LanguagePractice.Core.Interfaces;

public interface IGuestSessionService
{
    Task<GuestSession> CreateAsync(string displayName, string? preferredLanguageCode, TimeSpan lifetime, CancellationToken cancellationToken = default);
    Task<GuestSession?> ValidateAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task TouchAsync(Guid guestSessionId, CancellationToken cancellationToken = default);
}
