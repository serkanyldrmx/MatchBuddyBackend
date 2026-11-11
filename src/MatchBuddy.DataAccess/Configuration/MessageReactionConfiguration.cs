using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
    {
        public void Configure(EntityTypeBuilder<MessageReaction> builder)
        {
            builder.ToTable("MessageReactions");
            builder.HasKey(x => x.ReactionId);
            builder.Property(x => x.Emoji).HasMaxLength(16).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            // Oyuncu başına mesaj başına tek tepki
            builder.HasIndex(x => new { x.MessageId, x.PlayerId }).IsUnique().HasFilter("[MessageId] IS NOT NULL");
            builder.HasIndex(x => new { x.GroupMessageId, x.PlayerId }).IsUnique().HasFilter("[GroupMessageId] IS NOT NULL");

            builder.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<GroupMessage>().WithMany().HasForeignKey(x => x.GroupMessageId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
