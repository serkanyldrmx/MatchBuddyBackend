using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IStadiumService
    {
        IDataResult<List<Stadium>> GetAll();
        IDataResult<List<GetStadiumMatchModel>> GetStadiumMatch();

        IResult Add(Stadium stadium);
    }
}
