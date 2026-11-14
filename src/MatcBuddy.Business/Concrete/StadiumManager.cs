using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using System.Numerics;

namespace MatcBuddy.Business.Concrete
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
            _stadiumDal.AddNotificationStadium(stadium);
            return new Result(true, Messages.Added);
        }

        public IDataResult<List<GetStadiumMatchModel>> GetStadiumMatch()
        {
            return new SuccessDataResult<List<GetStadiumMatchModel>>(_stadiumDal.GetStadiumMatchs());
        }

        public IDataResult<List<Stadium>> GetAll()
        {
            return new SuccessDataResult<List<Stadium>>(_stadiumDal.GetAll(s => !s.IsExternal), Messages.PlayersListed);
        }
    }
}
