using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class GroupPlayerModel
    {
        public int GroupPlayerId { get; set; }
        public int GroupId { get; set; }
        public int? RecipientPlayerId { get; set; }
    }
}
