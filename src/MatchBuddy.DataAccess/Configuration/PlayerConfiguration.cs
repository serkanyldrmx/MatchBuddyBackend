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
                .HasMaxLength(20)
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

        }
    }
}
