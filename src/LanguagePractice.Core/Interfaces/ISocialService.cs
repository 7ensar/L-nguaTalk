using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Interfaces;

public interface ISocialService
{
    Task<Friendship?> RequestFriendAsync(string requesterId, string addresseeId, CancellationToken cancellationToken = default);
    Task<bool> RespondFriendAsync(string userId, Guid friendshipId, bool accept, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Friendship>> GetFriendsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Friendship>> GetPendingAsync(string userId, CancellationToken cancellationToken = default);
    Task BlockAsync(string blockerId, string? blockedUserId, Guid? blockedGuestId, string? reason, CancellationToken cancellationToken = default);
    Task UnblockAsync(string blockerId, Guid blockId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetBlockedUserIdsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> RateMatchAsync(string userId, string roomId, byte rating, CancellationToken cancellationToken = default);
    Task<bool> CompleteMatchAsync(string? userId, Guid? guestSessionId, string roomId, int durationSeconds, CancellationToken cancellationToken = default);
}
