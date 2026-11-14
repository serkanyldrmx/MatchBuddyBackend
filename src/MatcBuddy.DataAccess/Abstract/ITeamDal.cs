using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface ITeamDal : IEntityRepository<Team>
    {
        List<GetTeamAndPlayer> GetTeamAndPlayer();
        void SaveTeamByPlayerId(List<int> playerIds, int teamId);
        bool IsPlayerInTeam(int teamId, int playerId);
        // Takım herhangi bir turnuvaya kayıtlı mı (kayıtlıyken silinemez)
        bool IsInAnyTournament(int teamId);
        // Takımı ekler ve TeamCreated aktivitesini aynı transaction'da yazar
        void AddWithActivity(Team team, int creatorPlayerId);
        // Maçlardaki ev sahibi / rakip takım bağlantılarını kaldırır (FK NoAction; takım silinmeden önce)
        void ClearMatchReferences(int teamId);
        // Takımı kuran oyuncu (eski takımlarda null)
        int? GetCreatorId(int teamId);
        // Oyuncuyu takımdan çıkarır; kurucu ayrılırsa kuruculuk en eski üyeye geçer.
        // false: oyuncu takımda kalan son kişi (takım silinmeli)
        bool Leave(int teamId, int playerId);
    }
}
