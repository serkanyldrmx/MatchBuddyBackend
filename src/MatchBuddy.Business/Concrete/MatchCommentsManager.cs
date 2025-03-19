using System.Text.RegularExpressions;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
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

        public IResult Add(MatchComment matchComment)
        {
            // Maç ismi geçerli mi kontrol et
            //if (matchComment.Comment.Length < 5)
            //{
            //    return new ErrorResult(Messages.MatchNameInvalid);
            //}

            // Maçı ekle
            _matchCommentsDal.Add(matchComment);
            return new Result(true, Messages.Added);
        }

        public IResult Delete(MatchComment matchComment)
        {
            _matchCommentsDal.Delete(matchComment);
            return new Result(true, Messages.Deleted);
        }

        public IDataResult<List<MatchComment>> GetAll()
        {
            return new SuccessDataResult<List<MatchComment>>(_matchCommentsDal.GetAll(), Messages.PlayersListed);
        }

        public IDataResult<List<MatchComentsDto>> GetById(int matchId)
        {
            return new SuccessDataResult<List<MatchComentsDto>>(_matchCommentsDal.GetMatchCommentToMatchId(matchId));
        }
    }
}
