using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class GroupPlayer : IEntity
    {
        public int GroupPlayerId { get; set; }
        public int GroupId { get; set; }
        public int PlayerId { get; set; }
        public Group Group { get; set; }
        public Player Player { get; set; }
    }
}
