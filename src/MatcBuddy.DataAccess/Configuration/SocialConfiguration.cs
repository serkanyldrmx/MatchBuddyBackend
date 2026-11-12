using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    // Sosyal paket tabloları. Player FK'leri NoAction (çoklu cascade yolu hatasını önler).

    public class FollowConfiguration : IEntityTypeConfiguration<Follow>
    {
        public void Configure(EntityTypeBuilder<Follow> builder)
        {
            builder.ToTable("Follows");
            builder.HasKey(x => x.FollowId);
            builder.HasIndex(x => new { x.FollowerId, x.FolloweeId }).IsUnique();
            builder.HasIndex(x => x.FolloweeId);

            builder.HasOne(x => x.Follower).WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Followee).WithMany().HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class BlockConfiguration : IEntityTypeConfiguration<Block>
    {
        public void Configure(EntityTypeBuilder<Block> builder)
        {
            builder.ToTable("Blocks");
            builder.HasKey(x => x.BlockId);
            builder.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
            builder.HasIndex(x => x.BlockedId);

            builder.HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Blocked).WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class ReportConfiguration : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> builder)
        {
            builder.ToTable("Reports");
            builder.HasKey(x => x.ReportId);
            builder.Property(x => x.Reason).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Details).HasMaxLength(500);
            builder.HasIndex(x => x.ReportedPlayerId);
            builder.HasIndex(x => x.Status);

            builder.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.ReportedPlayer).WithMany().HasForeignKey(x => x.ReportedPlayerId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class PlayerSettingsConfiguration : IEntityTypeConfiguration<PlayerSettings>
    {
        public void Configure(EntityTypeBuilder<PlayerSettings> builder)
        {
            builder.ToTable("PlayerSettings");
            builder.HasKey(x => x.PlayerId);
            builder.Property(x => x.PlayerId).ValueGeneratedNever();
            builder.Property(x => x.Theme).HasMaxLength(10).IsRequired().HasDefaultValue("system");
            builder.Property(x => x.Language).HasMaxLength(10).IsRequired().HasDefaultValue("tr");

            // Not: bool alanlara HasDefaultValue(true) verilmez; EF 'false' değerini
            // "atanmamış" sayıp DB varsayılanını (true) yazardı. Varsayılanlar kodda.
            builder.HasOne(x => x.Player)
                .WithOne()
                .HasForeignKey<PlayerSettings>(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class MatchInviteConfiguration : IEntityTypeConfiguration<MatchInvite>
    {
        public void Configure(EntityTypeBuilder<MatchInvite> builder)
        {
            builder.ToTable("MatchInvites");
            builder.HasKey(x => x.InviteId);
            builder.HasIndex(x => new { x.MatchId, x.InviteeId }).IsUnique();
            builder.HasIndex(x => new { x.InviteeId, x.Status });

            builder.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Inviter).WithMany().HasForeignKey(x => x.InviterId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Invitee).WithMany().HasForeignKey(x => x.InviteeId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class TeamInviteConfiguration : IEntityTypeConfiguration<TeamInvite>
    {
        public void Configure(EntityTypeBuilder<TeamInvite> builder)
        {
            builder.ToTable("TeamInvites");
            builder.HasKey(x => x.InviteId);
            builder.HasIndex(x => new { x.TeamId, x.InviteeId }).IsUnique();
            builder.HasIndex(x => new { x.InviteeId, x.Status });

            builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Inviter).WithMany().HasForeignKey(x => x.InviterId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Invitee).WithMany().HasForeignKey(x => x.InviteeId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
    {
        public void Configure(EntityTypeBuilder<Activity> builder)
        {
            builder.ToTable("Activities");
            builder.HasKey(x => x.ActivityId);
            builder.HasIndex(x => new { x.ActorPlayerId, x.CreatedAt });
            builder.HasIndex(x => new { x.MatchId, x.Type });

            builder.HasOne(x => x.ActorPlayer).WithMany().HasForeignKey(x => x.ActorPlayerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.TargetPlayer).WithMany().HasForeignKey(x => x.TargetPlayerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            // Takım silinince o takımın aktiviteleri de silinir
            builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
