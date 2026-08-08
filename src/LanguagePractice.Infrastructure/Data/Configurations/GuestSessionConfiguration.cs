using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class GuestSessionConfiguration : IEntityTypeConfiguration<GuestSession>
{
    public void Configure(EntityTypeBuilder<GuestSession> builder)
    {
        builder.ToTable("GuestSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SessionToken).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PreferredLanguageCode).HasMaxLength(10);
        builder.HasIndex(x => x.SessionToken).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
