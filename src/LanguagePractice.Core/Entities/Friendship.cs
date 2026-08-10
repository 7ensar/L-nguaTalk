using LanguagePractice.Core.Enums;

namespace LanguagePractice.Core.Entities;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequesterId { get; set; } = string.Empty;
    public string AddresseeId { get; set; } = string.Empty;
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }

    public ApplicationUser Requester { get; set; } = null!;
    public ApplicationUser Addressee { get; set; } = null!;
}
