using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
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
    }
}
