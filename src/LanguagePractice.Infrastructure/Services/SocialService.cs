using LanguagePractice.Core.Entities;
using LanguagePractice.Core.Enums;
using LanguagePractice.Core.Interfaces;
using LanguagePractice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguagePractice.Infrastructure.Services;

public class SocialService : ISocialService
{
    private readonly ApplicationDbContext _db;

    public SocialService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Friendship?> RequestFriendAsync(string requesterId, string addresseeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterId) || string.IsNullOrWhiteSpace(addresseeId)
            || requesterId == addresseeId)
        {
            return null;
        }

        var blocked = await _db.UserBlocks.AsNoTracking().AnyAsync(x =>
            (x.BlockerUserId == requesterId && x.BlockedUserId == addresseeId)
            || (x.BlockerUserId == addresseeId && x.BlockedUserId == requesterId), cancellationToken);
        if (blocked)
        {
            return null;
        }

        var existing = await _db.Friendships.FirstOrDefaultAsync(x =>
            (x.RequesterId == requesterId && x.AddresseeId == addresseeId)
            || (x.RequesterId == addresseeId && x.AddresseeId == requesterId), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var row = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending
        };
        _db.Friendships.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    public async Task<bool> RespondFriendAsync(string userId, Guid friendshipId, bool accept, CancellationToken cancellationToken = default)
    {
        var row = await _db.Friendships.FirstOrDefaultAsync(
            x => x.Id == friendshipId && x.AddresseeId == userId && x.Status == FriendshipStatus.Pending,
            cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.Status = accept ? FriendshipStatus.Accepted : FriendshipStatus.Declined;
        row.RespondedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Friendship>> GetFriendsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.Friendships.AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.Addressee)
            .Where(x => x.Status == FriendshipStatus.Accepted
                        && (x.RequesterId == userId || x.AddresseeId == userId))
            .OrderByDescending(x => x.RespondedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Friendship>> GetPendingAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.Friendships.AsNoTracking()
            .Include(x => x.Requester)
            .Where(x => x.AddresseeId == userId && x.Status == FriendshipStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task BlockAsync(string blockerId, string? blockedUserId, Guid? blockedGuestId, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blockedUserId) && blockedGuestId is null)
        {
            return;
        }

        var exists = await _db.UserBlocks.AnyAsync(x =>
            x.BlockerUserId == blockerId
            && x.BlockedUserId == blockedUserId
            && x.BlockedGuestSessionId == blockedGuestId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.UserBlocks.Add(new UserBlock
        {
            BlockerUserId = blockerId,
            BlockedUserId = blockedUserId,
            BlockedGuestSessionId = blockedGuestId,
            Reason = reason?.Trim().Length > 0 ? reason.Trim()[..Math.Min(reason.Trim().Length, 500)] : null
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockAsync(string blockerId, Guid blockId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserBlocks.FirstOrDefaultAsync(
            x => x.Id == blockId && x.BlockerUserId == blockerId, cancellationToken);
        if (row is null)
        {
            return;
        }

        _db.UserBlocks.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetBlockedUserIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserBlocks.AsNoTracking()
            .Where(x => x.BlockerUserId == userId && x.BlockedUserId != null)
            .Select(x => x.BlockedUserId!)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RateMatchAsync(string userId, string roomId, byte rating, CancellationToken cancellationToken = default)
    {
        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        var match = await _db.MatchHistories.FirstOrDefaultAsync(x => x.RoomId == roomId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        if (match.UserAId == userId)
        {
            match.RatingByUserA = rating;
        }
        else if (match.UserBId == userId)
        {
            match.RatingByUserB = rating;
        }
        else
        {
            return false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteMatchAsync(string? userId, Guid? guestSessionId, string roomId, int durationSeconds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        var match = await _db.MatchHistories.FirstOrDefaultAsync(x => x.RoomId == roomId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        var allowed =
            (!string.IsNullOrEmpty(userId) && (match.UserAId == userId || match.UserBId == userId))
            || (guestSessionId.HasValue && (match.GuestSessionAId == guestSessionId || match.GuestSessionBId == guestSessionId));

        if (!allowed && !string.IsNullOrEmpty(userId) && match.UserAId is null && match.UserBId is null)
        {
            // Signaling henüz user id yazmadıysa oda sahibini kabul et
            allowed = true;
        }

        if (!allowed)
        {
            return false;
        }

        match.Status = MatchStatus.Completed;
        match.EndedAtUtc = DateTime.UtcNow;
        match.DurationSeconds = Math.Max(0, durationSeconds);
        match.StartedAtUtc ??= match.MatchedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
