using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
    {
        public void Configure(EntityTypeBuilder<EmailTemplate> builder)
        {
            builder.ToTable("EmailTemplates");
            builder.HasKey(x => x.EmailTemplateId);
            builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Body).HasMaxLength(5000).IsRequired();
            builder.Property(x => x.ButtonText).HasMaxLength(60);
            builder.Property(x => x.ButtonUrl).HasMaxLength(500);
            builder.HasIndex(x => new { x.OwnerPlayerId, x.Key }).IsUnique().HasFilter(null);
        }
    }

    public class EmailCampaignConfiguration : IEntityTypeConfiguration<EmailCampaign>
    {
        public void Configure(EntityTypeBuilder<EmailCampaign> builder)
        {
            builder.ToTable("EmailCampaigns");
            builder.HasKey(x => x.EmailCampaignId);
            builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Body).HasMaxLength(5000).IsRequired();
            builder.Property(x => x.ButtonText).HasMaxLength(60);
            builder.Property(x => x.ButtonUrl).HasMaxLength(500);
            builder.HasIndex(x => new { x.Status, x.ScheduledAt });
            builder.HasIndex(x => x.OwnerPlayerId);
        }
    }

    public class MatchEmailReminderConfiguration : IEntityTypeConfiguration<MatchEmailReminder>
    {
        public void Configure(EntityTypeBuilder<MatchEmailReminder> builder)
        {
            builder.ToTable("MatchEmailReminders");
            builder.HasKey(x => x.MatchEmailReminderId);
            builder.HasIndex(x => new { x.MatchId, x.PlayerId, x.MinutesBefore }).IsUnique();
            builder.HasIndex(x => x.SentAt);
        }
    }
}
