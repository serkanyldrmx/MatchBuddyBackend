using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class MatchConfiguration : IEntityTypeConfiguration<Match>
    {
        public void Configure(EntityTypeBuilder<Match> builder)
        {
            builder.HasKey(x => x.MatchId);

            builder
                .Property(b => b.MatchName)
                .HasMaxLength(100)
                .IsRequired();

            builder
                .Property(b => b.MatchDate)
                .IsRequired();

            builder
                .Property(b => b.UserCount)
                .IsRequired();

            builder
                .Property(b => b.Description)
                .HasMaxLength(500)
                .IsRequired();

            builder
                .Property(b => b.IsActive)
                .IsRequired();

            builder
                .Property(b => b.Likes)
                .IsRequired();

            builder.HasOne(x => x.Stadium)
                .WithMany(x => x.Matches)
                .HasPrincipalKey(x => x.StadiumId)
                .OnDelete(DeleteBehavior.Cascade);

            // Rezervasyon alanları (docs/venues-api.md)
            builder.Property(x => x.DurationMinutes).HasDefaultValue(60);
            builder.Property(x => x.CleatCount).HasDefaultValue(0);
            builder.Property(x => x.PitchPrice).HasColumnType("decimal(10,2)");
            builder.Property(x => x.ExtrasPrice).HasColumnType("decimal(10,2)");
            builder.Property(x => x.TotalPrice).HasColumnType("decimal(10,2)");
            builder.Property(x => x.RejectReason).HasMaxLength(200);
            builder.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            builder.HasIndex(x => new { x.PitchId, x.MatchDate });
            builder.HasIndex(x => x.SeriesId);
            builder.Property(x => x.ShareCode).HasMaxLength(12);
            builder.HasIndex(x => x.ShareCode).IsUnique().HasFilter("[ShareCode] IS NOT NULL");

            builder.HasOne<Pitch>().WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne<Team>().WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne<Team>().WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.NoAction);

            // Maçı oluşturan oyuncu (eski maçlarda null). NoAction: çoklu cascade yolu olmasın
            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

        }
    }
}
