using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Api.Model
{
    public class MatchCommentModel
    {
        public string? Comment { get; set; }
        public int playerId { get; set; }
        public int MatchId { get; set; }
    }
}
