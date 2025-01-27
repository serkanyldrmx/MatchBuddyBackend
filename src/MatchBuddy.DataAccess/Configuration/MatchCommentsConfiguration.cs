using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MatchBuddy.DataAccess.Configuration
{
    public class MatchCommentsConfiguration : IEntityTypeConfiguration<MatchComment>
    {
        public void Configure(EntityTypeBuilder<MatchComment> builder)
        {
            builder.HasKey(x => x.CommentsId);

            // UserId alanı için foreign key ilişkisi
            builder.HasOne(x => x.Player)
                   .WithMany(x => x.MatchComments)
                   .HasForeignKey(x => x.playerId)
                   .OnDelete(DeleteBehavior.Restrict);

            // MatchId alanı için foreign key ilişkisi
            builder.HasOne(x => x.Match)
                   .WithMany(x => x.MatchComments)
                   .HasForeignKey(x => x.MatchId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
