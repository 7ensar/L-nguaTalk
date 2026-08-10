using LanguagePractice.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguagePractice.Infrastructure.Data.Configurations;

public class ConversationTopicConfiguration : IEntityTypeConfiguration<ConversationTopic>
{
    public void Configure(EntityTypeBuilder<ConversationTopic> builder)
    {
        builder.ToTable("ConversationTopics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LanguageCode).HasMaxLength(10);
        builder.Property(x => x.TextEn).HasMaxLength(300);
        builder.Property(x => x.TextTr).HasMaxLength(300);

        builder.HasData(
            new ConversationTopic { Id = 1, LanguageCode = "*", TextEn = "What hobby makes you lose track of time?", TextTr = "Zamanı unutturan hobin nedir?", SortOrder = 1 },
            new ConversationTopic { Id = 2, LanguageCode = "*", TextEn = "Describe your perfect weekend.", TextTr = "Mükemmel bir hafta sonunu anlat.", SortOrder = 2 },
            new ConversationTopic { Id = 3, LanguageCode = "*", TextEn = "What are you learning right now besides languages?", TextTr = "Diller dışında şu an ne öğreniyorsun?", SortOrder = 3 },
            new ConversationTopic { Id = 4, LanguageCode = "*", TextEn = "Recommend a movie or series and why.", TextTr = "Bir film veya dizi öner ve nedenini söyle.", SortOrder = 4 },
            new ConversationTopic { Id = 5, LanguageCode = "*", TextEn = "What food from your culture should everyone try?", TextTr = "Kültüründen herkesin denemesi gereken yemek nedir?", SortOrder = 5 },
            new ConversationTopic { Id = 6, LanguageCode = "*", TextEn = "Tell a funny travel or school story.", TextTr = "Komik bir seyahat veya okul hikâyesi anlat.", SortOrder = 6 },
            new ConversationTopic { Id = 7, LanguageCode = "*", TextEn = "What goal are you working toward this year?", TextTr = "Bu yıl hangi hedefe çalışıyorsun?", SortOrder = 7 },
            new ConversationTopic { Id = 8, LanguageCode = "*", TextEn = "If you could live anywhere for a month, where?", TextTr = "Bir ay herhangi bir yerde yaşasan nerede olurdu?", SortOrder = 8 }
        );
    }
}
