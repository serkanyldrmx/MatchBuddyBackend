using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class Team : IEntity
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; }
        /// <summary>Takımı kuran oyuncu: takımı yalnızca o silebilir, kadroyu o düzenler.
        /// Eski takımlarda (kurucu kaydı bulunamayan) null: o takımlarda eski kural (her üye) geçerlidir.</summary>
        public int? CreatedByPlayerId { get; set; }
        public List<MatchTeam> MatchTeams { get; set; }
        public List<PlayerTeam> PlayerTeams { get; set; }
    }
}
