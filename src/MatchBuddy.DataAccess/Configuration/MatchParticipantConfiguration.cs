using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
    {
        public void Configure(EntityTypeBuilder<MatchParticipant> builder)
        {
            builder.HasKey(x => x.MatchParticipantId);

            builder.Property(x => x.Goals).HasDefaultValue(0);
            builder.Property(x => x.Assists).HasDefaultValue(0);

            // Bir oyuncu bir maçta tek kayıt
            builder.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();

            builder.HasOne(x => x.Match)
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Player FK'leri NoAction: çoklu cascade yolu hatasını önler
            builder.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class MatchLikeConfiguration : IEntityTypeConfiguration<MatchLike>
    {
        public void Configure(EntityTypeBuilder<MatchLike> builder)
        {
            builder.HasKey(x => x.MatchLikeId);
            builder.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();

            builder.HasOne(x => x.Match)
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class MvpVoteConfiguration : IEntityTypeConfiguration<MvpVote>
    {
        public void Configure(EntityTypeBuilder<MvpVote> builder)
        {
            builder.HasKey(x => x.MvpVoteId);
            builder.HasIndex(x => new { x.MatchId, x.VoterPlayerId }).IsUnique();

            builder.HasOne(x => x.Match)
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.VoterPlayer)
                .WithMany()
                .HasForeignKey(x => x.VoterPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.VotedPlayer)
                .WithMany()
                .HasForeignKey(x => x.VotedPlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class PlayerRatingConfiguration : IEntityTypeConfiguration<PlayerRating>
    {
        public void Configure(EntityTypeBuilder<PlayerRating> builder)
        {
            builder.HasKey(x => x.PlayerRatingId);
            builder.HasIndex(x => new { x.MatchId, x.RaterPlayerId, x.RatedPlayerId }).IsUnique();
            builder.HasIndex(x => x.RatedPlayerId);

            builder.HasOne(x => x.Match)
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.RaterPlayer)
                .WithMany()
                .HasForeignKey(x => x.RaterPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.RatedPlayer)
                .WithMany()
                .HasForeignKey(x => x.RatedPlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
