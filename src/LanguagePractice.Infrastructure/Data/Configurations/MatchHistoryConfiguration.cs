using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class MatchHistoryConfiguration : IEntityTypeConfiguration<MatchHistory>
{
    public void Configure(EntityTypeBuilder<MatchHistory> builder)
    {
        builder.ToTable("MatchHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RoomId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.UserA)
            .WithMany(x => x.MatchesAsUserA)
            .HasForeignKey(x => x.UserAId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.UserB)
            .WithMany(x => x.MatchesAsUserB)
            .HasForeignKey(x => x.UserBId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.GuestSessionA)
            .WithMany()
            .HasForeignKey(x => x.GuestSessionAId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.GuestSessionB)
            .WithMany()
            .HasForeignKey(x => x.GuestSessionBId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.PracticeLanguage)
            .WithMany()
            .HasForeignKey(x => x.PracticeLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RoomId).IsUnique();
        builder.HasIndex(x => x.QueuedAtUtc);
    }
}
