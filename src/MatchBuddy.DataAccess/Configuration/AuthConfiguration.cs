using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");
            builder.HasKey(x => x.RefreshTokenId);
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => new { x.PlayerId, x.RevokedAt });
            builder.HasIndex(x => x.FamilyId);
            builder.Property(x => x.RevokedReason).HasMaxLength(30).IsUnicode(false);
            builder.Property(x => x.CreatedByIp).HasMaxLength(45).IsUnicode(false);
            builder.Property(x => x.UserAgent).HasMaxLength(300);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
    {
        public void Configure(EntityTypeBuilder<ExternalLogin> builder)
        {
            builder.ToTable("ExternalLogins");
            builder.HasKey(x => x.ExternalLoginId);
            builder.Property(x => x.Provider).HasMaxLength(20).IsUnicode(false).IsRequired();
            builder.Property(x => x.ProviderKey).HasMaxLength(200).IsUnicode(false).IsRequired();
            builder.Property(x => x.Email).HasMaxLength(256);
            builder.Property(x => x.DisplayName).HasMaxLength(200);
            builder.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();
            // Bir oyuncunun her sağlayıcıda en fazla bir bağlantısı olur
            builder.HasIndex(x => new { x.PlayerId, x.Provider }).IsUnique();
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AuthCodeConfiguration : IEntityTypeConfiguration<AuthCode>
    {
        public void Configure(EntityTypeBuilder<AuthCode> builder)
        {
            builder.ToTable("AuthCodes");
            builder.HasKey(x => x.AuthCodeId);
            builder.Property(x => x.CodeHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            builder.Property(x => x.Destination).HasMaxLength(256).IsRequired();
            builder.Property(x => x.CreatedByIp).HasMaxLength(45).IsUnicode(false);
            builder.HasIndex(x => new { x.PlayerId, x.Purpose, x.CreatedAt });
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
