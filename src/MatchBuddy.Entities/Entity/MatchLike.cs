using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class MatchLike : IEntity
    {
        public int MatchLikeId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public int PlayerId { get; set; }
        public Player Player { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
