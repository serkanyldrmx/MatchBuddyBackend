using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MatchBuddy.DataAccess.Configuration
{
    public class StadiumConfiguration : IEntityTypeConfiguration<Stadium>
    {
        public void Configure(EntityTypeBuilder<Stadium> builder)
        {
            builder.HasKey(x => x.StadiumId);
            builder
                .Property(b => b.StadiumName)
                .HasMaxLength(250)
                .IsRequired();

            builder.Property(s => s.Location)
              .HasMaxLength(200);

            // İl / ilçe metin olarak (docs/venues-api.md)
            builder.Property(s => s.City)
                .HasMaxLength(60)
                .IsRequired(false);

            builder.Property(s => s.District)
                .HasMaxLength(60)
                .IsRequired(false);

            builder.Property(s => s.Address)
                .HasMaxLength(300);

            var timeOnlyConverter = new ValueConverter<TimeOnly, TimeSpan>(
                v => v.ToTimeSpan(),
                v => TimeOnly.FromTimeSpan(v));

            builder.Property(s => s.OpeningTime)
                .HasConversion(timeOnlyConverter)
                .IsRequired(false);

            builder.Property(s => s.ClosingTime)
                .HasConversion(timeOnlyConverter)
                .IsRequired(false);

            builder.Property(s => s.Description)
                .HasMaxLength(500);

            builder.Property(s => s.Phone).HasMaxLength(30);
            builder.Property(s => s.CleatRentalPrice).HasColumnType("decimal(10,2)");
            builder.Property(s => s.CameraPrice).HasColumnType("decimal(10,2)");
            builder.Property(s => s.CancellationPolicy).HasMaxLength(500);
            builder.HasIndex(s => s.OwnerPlayerId);
            builder.HasIndex(s => s.OsmId);

            // Harici (anlaşmasız) saha: oyuncunun eklediği yer (docs/external-venues-api.md)
            builder.Property(s => s.IsExternal).HasDefaultValue(false);
            builder.HasIndex(s => new { s.CreatedByPlayerId, s.IsExternal });
            builder.HasOne<Stadium>()
                .WithMany()
                .HasForeignKey(s => s.LinkedStadiumId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(s => s.CreatedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            // İşletme hesabı: NoAction (çoklu cascade yolu olmasın)
            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(s => s.OwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relationships
            builder.HasMany(s => s.Matches)
                .WithOne(m => m.Stadium)
                .HasForeignKey(m => m.StadiumId);
        }
    }
}
