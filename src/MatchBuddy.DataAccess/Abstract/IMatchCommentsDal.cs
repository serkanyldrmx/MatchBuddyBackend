using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IMatchCommentsDal:IEntityRepository<MatchComment>
    {
        List<MatchComentsDto> GetMatchCommentToMatchId(int matchId);
    }
}
