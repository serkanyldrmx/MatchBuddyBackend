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

            builder.HasOne(x => x.Stadium)
                .WithMany(x => x.Matches)
                .HasPrincipalKey(x => x.StadiumId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
