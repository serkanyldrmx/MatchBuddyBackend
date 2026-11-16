using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
    {
        public void Configure(EntityTypeBuilder<ContactMessage> builder)
        {
            builder.ToTable("ContactMessages");
            builder.HasKey(x => x.ContactMessageId);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Email).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Subject).HasMaxLength(150);
            builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            // Oyuncu silinse de mesaj kalır (hesap silme anonimleştirir, satırı silmez)
            builder.HasIndex(x => x.PlayerId);
            builder.HasIndex(x => new { x.IsHandled, x.CreatedAt });
        }
    }
}
