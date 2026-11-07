using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    /// <summary>Haftalık sabit maç serisi (docs/venue-growth-api.md).</summary>
    public class MatchSeriesConfiguration : IEntityTypeConfiguration<MatchSeries>
    {
        public void Configure(EntityTypeBuilder<MatchSeries> builder)
        {
            builder.ToTable("MatchSeries");
            builder.HasKey(x => x.SeriesId);
            builder.HasIndex(x => new { x.IsActive, x.NextDate });
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.CreatedByPlayerId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Pitch>().WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
