using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
{
    public interface IStadiumService
    {
        IDataResult<List<Stadium>> GetAll();
        IDataResult<List<GetStadiumMatchModel>> GetStadiumMatch();

        IResult Add(Stadium stadium);
    }
}
