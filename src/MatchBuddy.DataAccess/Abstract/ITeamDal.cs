using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface ITeamDal : IEntityRepository<Team>
    {
        List<GetTeamAndPlayer> GetTeamAndPlayer();
        void SaveTeamByPlayerId(List<int> playerIds, int teamId);
    }
}
