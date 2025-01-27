using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class PlayerTeam : IEntity
    {
        public int PlayerTeamId { get; set; }
        public Team Team { get; set; }
        public int TeamId { get; set; }
        public Player Player { get; set; }
        public int PlayerId { get; set; }
    }
}
