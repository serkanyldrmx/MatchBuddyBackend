using MatchBuddy.Entities;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class PlayerTeamConfiguration : IEntityTypeConfiguration<PlayerTeam>
    {
        public void Configure(EntityTypeBuilder<PlayerTeam> builder)
        {
            builder.HasKey(x => x.PlayerTeamId);           

            builder.HasOne(x => x.Player)
                .WithMany(x => x.PlayerTeams)
                .HasPrincipalKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Team)
                .WithMany(x => x.PlayerTeams)
                .HasPrincipalKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
