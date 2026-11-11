using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class PlayerConfiguration : IEntityTypeConfiguration<Player>
    {
        public void Configure(EntityTypeBuilder<Player> builder)
        {
            builder.HasKey(x => x.PlayerId);

            builder
                .Property(b => b.PlayerName)
                .HasMaxLength(100)
                .IsRequired();

            builder
                .Property(b => b.PlayerSurname)
                .HasMaxLength(100)
                .IsRequired();

            builder
                .Property(b => b.UserName)
                .HasMaxLength(25)
                .IsRequired();

            builder
                .Property(b => b.Password)
                .HasMaxLength(100) // BCrypt hash (60 karakter) için
                .IsRequired();

            builder
                .Property(b => b.Email)
                .HasMaxLength(100)
                .IsRequired();

            builder
                .Property(b => b.PhoneNumber)
                .HasMaxLength(100)
                .IsRequired();

            builder
                .Property(b => b.Address)
                .HasMaxLength(200)
                .IsRequired();

            builder
                .Property(b => b.Size)
                .IsRequired();

            builder.Property(b => b.Weight)
                .IsRequired();

            builder.Property(b => b.Age)
                .IsRequired();

            builder.Property(b => b.IsAdmin)
                 .IsRequired();

            builder.Property(b => b.UserScore)
                .IsRequired();

            // 0 oyuncu, 1 admin, 2 işletme
            builder.Property(b => b.Role).HasDefaultValue((byte)0);

            // Hesap güvenliği / KVKK (docs/auth-api.md). HasPassword için mevcut satırlar migration'da 1 ile doldurulur;
            // modelde varsayılan tanımlanmaz ki EF yeni kayıtlarda değeri her zaman göndersin (false da yazılabilsin).
            builder.Property(b => b.TermsVersion).HasMaxLength(20);
            builder.Property(b => b.ReliabilityScore).HasDefaultValue(100);

        }
    }
}
