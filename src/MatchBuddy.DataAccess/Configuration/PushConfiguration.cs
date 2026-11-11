using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class PlayerNotificationPushConfiguration : IEntityTypeConfiguration<PlayerNotification>
    {
        public void Configure(EntityTypeBuilder<PlayerNotification> builder)
        {
            // Gönderim kuyruğu: yalnızca bekleyen satırlar indekslenir
            builder.HasIndex(x => x.PushedAt).HasFilter("[PushedAt] IS NULL");
        }
    }

    public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
    {
        public void Configure(EntityTypeBuilder<PushSubscription> builder)
        {
            builder.ToTable("PushSubscriptions");
            builder.HasKey(x => x.PushSubscriptionId);
            builder.Property(x => x.Endpoint).HasMaxLength(800).IsUnicode(false).IsRequired();
            builder.HasIndex(x => x.Endpoint).IsUnique();
            builder.HasIndex(x => x.PlayerId);
            builder.Property(x => x.P256dh).HasMaxLength(200).IsUnicode(false).IsRequired();
            builder.Property(x => x.Auth).HasMaxLength(100).IsUnicode(false).IsRequired();
            builder.Property(x => x.UserAgent).HasMaxLength(300);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
