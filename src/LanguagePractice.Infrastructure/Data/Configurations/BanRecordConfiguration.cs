using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class BanRecordConfiguration : IEntityTypeConfiguration<BanRecord>
{
    public void Configure(EntityTypeBuilder<BanRecord> builder)
    {
        builder.ToTable("BanRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PeerKey).HasMaxLength(128);

        builder.HasOne(x => x.User)
            .WithMany(x => x.BanRecords)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.GuestSession)
            .WithMany()
            .HasForeignKey(x => x.GuestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RelatedReport)
            .WithMany()
            .HasForeignKey(x => x.RelatedReportId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => x.PeerKey);
    }
}
