using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("UserBlocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.BlockerUserId, x.BlockedUserId });
        builder.HasIndex(x => new { x.BlockerUserId, x.BlockedGuestSessionId });

        builder.HasOne(x => x.Blocker)
            .WithMany()
            .HasForeignKey(x => x.BlockerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.BlockedUser)
            .WithMany()
            .HasForeignKey(x => x.BlockedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
