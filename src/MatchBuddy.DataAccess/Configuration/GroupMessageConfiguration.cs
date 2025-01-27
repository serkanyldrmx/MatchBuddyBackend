using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
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
                .HasMaxLength(250)
                .IsRequired();

            builder
                .Property(b => b.   SendingDate)
                .IsRequired();

            builder.Property(b => b.SendPlayerId)
                .IsRequired();

            builder
                .Property(b => b.Status)
                .IsRequired();



        }
    }
}
