using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.DTOs
{
    public class MatchComentsDto:IDto
    {
        public int CommentsId { get; set; }
        public string? Comment { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public int PlayerId { get; set; }
        public int MatchId { get; set; }
    }
}
