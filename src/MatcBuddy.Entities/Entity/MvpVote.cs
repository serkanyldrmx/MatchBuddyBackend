using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class MvpVote : IEntity
    {
        public int MvpVoteId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public int VoterPlayerId { get; set; }
        public Player VoterPlayer { get; set; }
        public int VotedPlayerId { get; set; }
        public Player VotedPlayer { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
