using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class MatchModel
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int UserCount { get; set; }
        public string? Description { get; set; }
        public byte IsActive { get; set; }
        public int StadiumId { get; set; }
        //public Stadium Stadium { get; set; }
    }
}
