using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IMatchCommentsDal:IEntityRepository<MatchComment>
    {
        List<MatchComentsDto> GetMatchCommentToMatchId(int matchId);
    }
}
