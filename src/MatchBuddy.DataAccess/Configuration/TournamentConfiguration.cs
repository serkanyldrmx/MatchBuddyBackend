using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    // Turnuva tabloları. Player/Team FK'leri NoAction (çoklu cascade yolu hatasını önler);
    // turnuva silinince kayıtlar ve fikstürler cascade ile silinir.

    public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
    {
        public void Configure(EntityTypeBuilder<Tournament> builder)
        {
            builder.ToTable("Tournaments");
            builder.HasKey(x => x.TournamentId);
            builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.StartDate).HasColumnType("datetime");
            builder.HasIndex(x => new { x.Status, x.StartDate });

            builder.HasOne(x => x.Stadium).WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.CreatedByPlayer).WithMany().HasForeignKey(x => x.CreatedByPlayerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.WinnerTeam).WithMany().HasForeignKey(x => x.WinnerTeamId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class TournamentTeamConfiguration : IEntityTypeConfiguration<TournamentTeam>
    {
        public void Configure(EntityTypeBuilder<TournamentTeam> builder)
        {
            builder.ToTable("TournamentTeams");
            builder.HasKey(x => x.TournamentTeamId);
            builder.HasIndex(x => new { x.TournamentId, x.TeamId }).IsUnique();
            builder.HasIndex(x => x.TeamId);

            builder.HasOne(x => x.Tournament).WithMany(t => t.Teams).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class TournamentFixtureConfiguration : IEntityTypeConfiguration<TournamentFixture>
    {
        public void Configure(EntityTypeBuilder<TournamentFixture> builder)
        {
            builder.ToTable("TournamentFixtures");
            builder.HasKey(x => x.FixtureId);
            builder.Property(x => x.MatchDate).HasColumnType("datetime");
            builder.HasIndex(x => new { x.TournamentId, x.Round, x.Position }).IsUnique();

            builder.HasOne(x => x.Tournament).WithMany(t => t.Fixtures).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.HomeTeam).WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.AwayTeam).WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.WinnerTeam).WithMany().HasForeignKey(x => x.WinnerTeamId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
