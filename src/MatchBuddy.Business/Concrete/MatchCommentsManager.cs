using MatchBuddy.Business.Abstract;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class MatchCommentsManager: IMatchCommentsService
    {
        IMatchCommentsDal _matchCommentsDal;
        public MatchCommentsManager(IMatchCommentsDal matchCommentsDal)
        {
            _matchCommentsDal = matchCommentsDal;   
        }
        //public IDataResult<List<MatchComment>> GetMatchComment(int matchId)
        //{
        //    return new SuccessDataResult<Player>(_matchCommentsDal.Get(p => p.MatchId == matchId));
        //}
    }
}
