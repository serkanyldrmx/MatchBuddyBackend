using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class MatchTeam : IEntity
    {
        public int MatchTeamId { get; set; }
        public int TeamId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public Team Team { get; set; }
    }
}
