using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class PlayerRating : IEntity
    {
        public int PlayerRatingId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public int RaterPlayerId { get; set; }
        public Player RaterPlayer { get; set; }
        public int RatedPlayerId { get; set; }
        public Player RatedPlayer { get; set; }

        // 1–5
        public byte Score { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
