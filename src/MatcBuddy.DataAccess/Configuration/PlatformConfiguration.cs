using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
    {
        public void Configure(EntityTypeBuilder<SystemParameter> builder)
        {
            builder.ToTable("SystemParameters");
            builder.HasKey(x => x.SystemParameterId);
            builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.Key).IsUnique();
            builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
            builder.Property(x => x.DataType).HasConversion<byte>();
            builder.Property(x => x.Group).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
            builder.Property(x => x.MinValue).HasMaxLength(50);
            builder.Property(x => x.MaxValue).HasMaxLength(50);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.UpdatedByPlayerId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class ParameterAuditConfiguration : IEntityTypeConfiguration<ParameterAudit>
    {
        public void Configure(EntityTypeBuilder<ParameterAudit> builder)
        {
            builder.ToTable("ParameterAudits");
            builder.HasKey(x => x.ParameterAuditId);
            builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
            builder.Property(x => x.OldValue).HasMaxLength(4000);
            builder.Property(x => x.NewValue).HasMaxLength(4000).IsRequired();
            builder.HasIndex(x => new { x.SystemParameterId, x.ChangedAt });
            builder.HasOne<SystemParameter>().WithMany().HasForeignKey(x => x.SystemParameterId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.ChangedByPlayerId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class VenueSettingsConfiguration : IEntityTypeConfiguration<VenueSettings>
    {
        public void Configure(EntityTypeBuilder<VenueSettings> builder)
        {
            builder.ToTable("VenueSettings");
            builder.HasKey(x => x.StadiumId);
            builder.Property(x => x.StadiumId).ValueGeneratedNever();
            builder.HasOne<Stadium>().WithOne().HasForeignKey<VenueSettings>(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.UpdatedByPlayerId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
