using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using System.Numerics;

namespace MatchBuddy.Business.Concrete
{
    public class StadiumManager : IStadiumService
    {
        IStadiumDal _stadiumDal;
        public StadiumManager(IStadiumDal stadiumDal) 
        {
            _stadiumDal = stadiumDal;
        }

        public IResult Add(Stadium stadium)
        {
            if (stadium.StadiumName.Length < 3)
            {
                return new ErrorResult(Messages.PlayerNameInvalid);
            }
            _stadiumDal.Add(stadium);
            return new Result(true, Messages.Added);
        }

        public IDataResult<List<GetStadiumMatchModel>> GetStadiumMatch()
        {
            return new SuccessDataResult<List<GetStadiumMatchModel>>(_stadiumDal.GetStadiumMatchs());
        }

        public IDataResult<List<Stadium>> GetAll()
        {
            return new SuccessDataResult<List<Stadium>>(_stadiumDal.GetAll(), Messages.PlayersListed);
        }
    }
}
