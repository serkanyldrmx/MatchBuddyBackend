using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IMatchDal : IEntityRepository<Match>
    {
        List<MatchComentsDto> GetMatchComents(int matchId);
        List<MatchTeamDto> GetMatchTeam(int matchId);
        void MatchStatusUpdate(int matchId, byte status);
        List<Match> GetMatchToDate(Match match);
    }
}
