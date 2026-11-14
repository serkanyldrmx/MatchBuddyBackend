using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IMatchCommentsService
    {
        IDataResult<List<MatchComment>> GetAll();
        IResult Add(MatchComment matchComment);
        IResult Delete(MatchComment matchComment);
        MatchComment? GetComment(int commentId);
        IDataResult<List<MatchComentsDto>> GetById(int matchId);

        //IDataResult<List<MatchComment>> GetMatchComment(int matchId);
        //Maç yorumlarını getir
    }
}
