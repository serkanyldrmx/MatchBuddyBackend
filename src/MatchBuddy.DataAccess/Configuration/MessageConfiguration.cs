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
                .HasMaxLength(250)
                .IsRequired();

            builder
                .Property(b => b.SendingDate)
                .IsRequired();

            builder
                .Property(b => b.Status)
                .IsRequired();


            builder.Property(x=>x.RecipientPlayerId).IsRequired(false);

        }
    }
}
