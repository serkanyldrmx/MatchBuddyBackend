using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
{
    public interface IMatchCommentsService
    {
        IDataResult<List<MatchComment>> GetAll();
        IResult Add(MatchComment matchComment);
        IResult Delete(MatchComment matchComment);
        IDataResult<List<MatchComentsDto>> GetById(int matchId);

        //IDataResult<List<MatchComment>> GetMatchComment(int matchId);
        //Maç yorumlarını getir
    }
}
