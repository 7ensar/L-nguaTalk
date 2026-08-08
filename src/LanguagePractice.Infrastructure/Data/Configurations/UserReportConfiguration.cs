using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.ToTable("UserReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReasonCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.AdminNotes).HasMaxLength(2000);
        builder.Property(x => x.ReportedPeerSocketId).HasMaxLength(128);
        builder.Property(x => x.ReportedPeerDisplayName).HasMaxLength(80);
        builder.Property(x => x.RoomId).HasMaxLength(64);
        builder.Property(x => x.AutoAction).HasMaxLength(80);
        builder.Property(x => x.ReporterIpHash).HasMaxLength(128);

        builder.HasOne(x => x.ReporterUser)
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ReportedUser)
            .WithMany(x => x.ReportsReceived)
            .HasForeignKey(x => x.ReportedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ReporterGuestSession)
            .WithMany()
            .HasForeignKey(x => x.ReporterGuestSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ReportedGuestSession)
            .WithMany()
            .HasForeignKey(x => x.ReportedGuestSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.MatchHistory)
            .WithMany()
            .HasForeignKey(x => x.MatchHistoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.RoomId);
    }
}
