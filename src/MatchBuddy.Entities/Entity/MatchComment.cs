using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class MatchComment : IEntity
    {
        public int CommentsId { get; set; }
        public string? Comment { get; set; }
        public Player Player { get; set; }
        public int playerId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
    }
}
