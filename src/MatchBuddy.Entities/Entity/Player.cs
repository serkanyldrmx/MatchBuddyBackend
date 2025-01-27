using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Player : IEntity
    {
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public int Age { get; set; }
        public byte IsAdmin { get; set; }
        public int UserScore { get; set; }
        public List<MatchComment> MatchComments { get; set; }
        public List<PlayerTeam> PlayerTeams { get; set; }
        public List<GroupPlayer> GroupPlayers { get; set; }
    }
}
