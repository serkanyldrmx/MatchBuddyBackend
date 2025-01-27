using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class GroupPlayerConfiguration : IEntityTypeConfiguration<GroupPlayer>
    {
        public void Configure(EntityTypeBuilder<GroupPlayer> builder)
        {
            builder.HasKey(x => x.GroupPlayerId);

            builder.HasOne(x => x.Group)
               .WithMany(x => x.GroupPlayers)
               .HasPrincipalKey(x => x.GroupId)
               .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Player)
                .WithMany(x => x.GroupPlayers)
                .HasPrincipalKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
