using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class MatchTeamConfiguration : IEntityTypeConfiguration<MatchTeam>
    {
        public void Configure(EntityTypeBuilder<MatchTeam> builder)
        {
            builder.HasKey(x => x.MatchTeamId);           

            builder.HasOne(x => x.Match)
                .WithMany(x => x.MatchTeams)
                .HasPrincipalKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Team)
                .WithMany(x => x.MatchTeams)
                .HasPrincipalKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
