using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.HasKey(x => x.MessageId);

            builder
                .Property(b => b.MatchMessage)
                .HasMaxLength(1000)
                .IsRequired();

            builder
                .Property(b => b.SendingDate)
                .IsRequired();

            builder
                .Property(b => b.Status)
                .IsRequired();


            builder.Property(x=>x.RecipientPlayerId).IsRequired(false);

            // Resimli mesaj (docs/media-api.md); sadece resimli mesajda MatchMessage "" olur
            builder.Property(x => x.ImageUrl).HasMaxLength(300);

            // Okunmamış sayısı / konuşma yükleme: alıcı + gönderen
            builder.HasIndex(x => new { x.RecipientPlayerId, x.SendPlayerId });
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.HasOne<Message>().WithMany().HasForeignKey(x => x.ReplyToMessageId).OnDelete(DeleteBehavior.NoAction);

        }
    }
}
