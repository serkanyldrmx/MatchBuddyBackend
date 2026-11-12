using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    // Anlaşmalı tesis tabloları (docs/venues-api.md). Tesise bağlı satırlar Cascade;
    // Player / Match / Pitch'e ikinci yol olan FK'ler NoAction (çoklu cascade yolu hatasını önler).

    public class StadiumPhotoConfiguration : IEntityTypeConfiguration<StadiumPhoto>
    {
        public void Configure(EntityTypeBuilder<StadiumPhoto> builder)
        {
            builder.ToTable("StadiumPhotos");
            builder.HasKey(x => x.PhotoId);
            builder.Property(x => x.Url).HasMaxLength(300).IsRequired();
            builder.HasIndex(x => new { x.StadiumId, x.SortOrder });
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PitchConfiguration : IEntityTypeConfiguration<Pitch>
    {
        public void Configure(EntityTypeBuilder<Pitch> builder)
        {
            builder.ToTable("Pitches");
            builder.HasKey(x => x.PitchId);
            builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
            builder.Property(x => x.BasePrice).HasColumnType("decimal(10,2)");
            builder.HasIndex(x => x.StadiumId);
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class OpeningHourConfiguration : IEntityTypeConfiguration<OpeningHour>
    {
        public void Configure(EntityTypeBuilder<OpeningHour> builder)
        {
            builder.ToTable("OpeningHours");
            builder.HasKey(x => x.OpeningHourId);
            builder.Property(x => x.OpenTime).HasColumnType("time");
            builder.Property(x => x.CloseTime).HasColumnType("time");
            builder.HasIndex(x => new { x.StadiumId, x.DayOfWeek }).IsUnique();
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
    {
        public void Configure(EntityTypeBuilder<PriceRule> builder)
        {
            builder.ToTable("PriceRules");
            builder.HasKey(x => x.PriceRuleId);
            builder.Property(x => x.PricePerHour).HasColumnType("decimal(10,2)");
            builder.HasIndex(x => x.StadiumId);
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Pitch>().WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
    {
        public void Configure(EntityTypeBuilder<Campaign> builder)
        {
            builder.ToTable("Campaigns");
            builder.HasKey(x => x.CampaignId);
            builder.Property(x => x.Title).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(300);
            builder.Property(x => x.StartDate).HasColumnType("date");
            builder.Property(x => x.EndDate).HasColumnType("date");
            builder.Property(x => x.FixedPrice).HasColumnType("decimal(10,2)");
            builder.HasIndex(x => x.StadiumId);
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Pitch>().WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class SlotBlockConfiguration : IEntityTypeConfiguration<SlotBlock>
    {
        public void Configure(EntityTypeBuilder<SlotBlock> builder)
        {
            builder.ToTable("SlotBlocks");
            builder.HasKey(x => x.SlotBlockId);
            builder.Property(x => x.Reason).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Kind).HasDefaultValue((byte)0);
            builder.Property(x => x.CustomerName).HasMaxLength(80);
            builder.Property(x => x.CustomerPhone).HasMaxLength(30);
            builder.Property(x => x.Price).HasColumnType("decimal(10,2)");
            builder.Property(x => x.IsPaid).HasDefaultValue(false);
            builder.Property(x => x.Note).HasMaxLength(200);
            builder.HasIndex(x => new { x.PitchId, x.StartAt });
            builder.HasOne<Pitch>().WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VenueExpenseConfiguration : IEntityTypeConfiguration<VenueExpense>
    {
        public void Configure(EntityTypeBuilder<VenueExpense> builder)
        {
            builder.ToTable("VenueExpenses");
            builder.HasKey(x => x.ExpenseId);
            builder.Property(x => x.Date).HasColumnType("date");
            builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
            builder.Property(x => x.Description).HasMaxLength(200);
            builder.HasIndex(x => new { x.StadiumId, x.Date });
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VenueReviewConfiguration : IEntityTypeConfiguration<VenueReview>
    {
        public void Configure(EntityTypeBuilder<VenueReview> builder)
        {
            builder.ToTable("VenueReviews");
            builder.HasKey(x => x.ReviewId);
            builder.Property(x => x.Comment).HasMaxLength(500);
            builder.Property(x => x.OwnerReply).HasMaxLength(500);
            builder.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();
            builder.HasIndex(x => new { x.StadiumId, x.CreatedAt });
            builder.HasOne<Stadium>().WithMany().HasForeignKey(x => x.StadiumId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Player>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne<Match>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
