using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MatchBuddy.DataAccess.Configuration
{
    public class StadiumConfiguration : IEntityTypeConfiguration<Stadium>
    {
        public void Configure(EntityTypeBuilder<Stadium> builder)
        {
            builder.HasKey(x => x.StadiumId);
            builder
                .Property(b => b.StadiumName)
                .HasMaxLength(250)
                .IsRequired();

            builder.Property(s => s.Location)
              .HasMaxLength(200);

            builder.Property(s => s.City)
                .IsRequired(false);

            builder.Property(s => s.District)
                .IsRequired(false);

            builder.Property(s => s.Address)
                .HasMaxLength(300);

            var timeOnlyConverter = new ValueConverter<TimeOnly, TimeSpan>(
                v => v.ToTimeSpan(),
                v => TimeOnly.FromTimeSpan(v));

            builder.Property(s => s.OpeningTime)
                .HasConversion(timeOnlyConverter)
                .IsRequired(false);

            builder.Property(s => s.ClosingTime)
                .HasConversion(timeOnlyConverter)
                .IsRequired(false);

            builder.Property(s => s.Description)
                .HasMaxLength(500);

            // Relationships
            builder.HasMany(s => s.Matches)
                .WithOne(m => m.Stadium)
                .HasForeignKey(m => m.StadiumId);
        }
    }
}
