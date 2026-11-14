using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatcBuddy.DataAccess.Configuration
{
    public class TeamConfiguration : IEntityTypeConfiguration<Team>
    {
        public void Configure(EntityTypeBuilder<Team> builder)
        {
            builder.HasKey(x => x.TeamId);

            builder
                .Property(b => b.TeamName)
                .HasMaxLength(50)
                .IsRequired();
        }
    }
}
