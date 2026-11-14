using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    // Medya tablosu (docs/media-api.md). Bağlantı kolonları (Players.ProfileMediaId, StadiumPhotos.MediaId,
    // Messages.MediaId, GroupMessages.MediaId) bilinçli olarak FK değildir; silme davranışı MediaService'tedir.
    public class MediaConfiguration : IEntityTypeConfiguration<Media>
    {
        public void Configure(EntityTypeBuilder<Media> builder)
        {
            builder.ToTable("Media");
            builder.HasKey(x => x.MediaId);
            builder.Property(x => x.Kind).HasConversion<byte>().IsRequired();
            builder.Property(x => x.Status).HasConversion<byte>().IsRequired();
            builder.Property(x => x.KeyPrefix).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => x.KeyPrefix).IsUnique();
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.OwnerPlayerId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
