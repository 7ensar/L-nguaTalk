namespace LanguagePractice.Core.Entities;

public class UserBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BlockerUserId { get; set; } = string.Empty;
    public string? BlockedUserId { get; set; }
    public Guid? BlockedGuestSessionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    public ApplicationUser Blocker { get; set; } = null!;
    public ApplicationUser? BlockedUser { get; set; }
}
