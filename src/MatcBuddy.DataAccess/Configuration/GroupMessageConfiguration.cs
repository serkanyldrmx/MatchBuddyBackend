using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class GroupMessageConfiguration : IEntityTypeConfiguration<GroupMessage>
    {
        public void Configure(EntityTypeBuilder<GroupMessage> builder)
        {
            builder.HasKey(x => x.GroupMessageId);

            builder.HasOne(x => x.Group)
                   .WithMany(x => x.GroupMessages)
                   .HasForeignKey(x => x.GroupId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder
                .Property(b => b.MatchMessage)
                .HasMaxLength(1000)
                .IsRequired();

            builder
                .Property(b => b.   SendingDate)
                .IsRequired();

            builder.Property(b => b.SendPlayerId)
                .IsRequired();

            builder
                .Property(b => b.Status)
                .IsRequired();

            // Resimli mesaj (docs/media-api.md); sadece resimli mesajda MatchMessage "" olur
            builder.Property(x => x.ImageUrl).HasMaxLength(300);
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.HasOne<GroupMessage>().WithMany().HasForeignKey(x => x.ReplyToGroupMessageId).OnDelete(DeleteBehavior.NoAction);



        }
    }
}
