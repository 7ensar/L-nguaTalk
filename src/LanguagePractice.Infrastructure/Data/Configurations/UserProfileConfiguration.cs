using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Bio).HasMaxLength(1000);
        builder.Property(x => x.CountryCode).HasMaxLength(2);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.NativeLanguageCode).HasMaxLength(10);
        builder.Property(x => x.TargetLanguageCode).HasMaxLength(10);
        builder.Property(x => x.LanguageLevel).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.Interests).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithOne(x => x.Profile)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
