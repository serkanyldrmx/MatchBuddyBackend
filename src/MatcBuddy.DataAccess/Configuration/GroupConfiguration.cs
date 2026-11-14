using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class GroupConfiguration : IEntityTypeConfiguration<Group>
    {
        public void Configure(EntityTypeBuilder<Group> builder)
        {
            builder.HasKey(x => x.GroupId);

            builder
                .Property(b => b.GroupName)
                .HasMaxLength(250)
                .IsRequired();

            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
