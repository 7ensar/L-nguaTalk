using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class InterestTagConfiguration : IEntityTypeConfiguration<InterestTag>
{
    public void Configure(EntityTypeBuilder<InterestTag> builder)
    {
        builder.ToTable("InterestTags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        var seed = new (string Slug, string Name)[]
        {
            ("music", "Music"),
            ("movies", "Movies"),
            ("travel", "Travel"),
            ("sports", "Sports"),
            ("gaming", "Gaming"),
            ("food", "Food"),
            ("books", "Books"),
            ("tech", "Tech"),
            ("art", "Art"),
            ("fashion", "Fashion"),
            ("nature", "Nature"),
            ("comedy", "Comedy")
        };

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(seed.Select((x, i) => new InterestTag
        {
            Id = i + 1,
            Slug = x.Slug,
            DisplayName = x.Name,
            IsActive = true,
            SortOrder = i + 1,
            CreatedAtUtc = now
        }));
    }
}
