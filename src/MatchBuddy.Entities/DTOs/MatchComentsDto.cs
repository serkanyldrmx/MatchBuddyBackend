using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.DTOs
{
    public class MatchComentsDto:IDto
    {
        public int PlayerId { get; set; }
        public int MatchId { get; set; }
        public string Comment { get; set; }
    }
}
