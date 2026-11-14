using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class BusinessApplicationConfiguration : IEntityTypeConfiguration<BusinessApplication>
    {
        public void Configure(EntityTypeBuilder<BusinessApplication> builder)
        {
            builder.ToTable("BusinessApplications");
            builder.HasKey(x => x.BusinessApplicationId);
            builder.Property(x => x.BusinessName).HasMaxLength(120).IsRequired();
            builder.Property(x => x.ContactName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            builder.Property(x => x.Email).HasMaxLength(150);
            builder.Property(x => x.City).HasMaxLength(60);
            builder.Property(x => x.District).HasMaxLength(60);
            builder.Property(x => x.Note).HasMaxLength(1000);
            builder.Property(x => x.AdminNote).HasMaxLength(1000);
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
        }
    }
}
