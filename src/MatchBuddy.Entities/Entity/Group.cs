using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Group : IEntity
    {
        public int GroupId { get; set; }
        public string? GroupName { get; set; }
        // Grubu kuran oyuncu (üye çıkarma yetkisi; eski gruplarda null)
        public int? CreatedByPlayerId { get; set; }
        public List<GroupMessage> GroupMessages { get; set; }
        public List<GroupPlayer> GroupPlayers { get; set; }
    }
}
