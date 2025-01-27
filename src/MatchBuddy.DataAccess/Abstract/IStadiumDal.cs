using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IStadiumDal : IEntityRepository<Stadium>
    {
        List<GetStadiumMatchModel> GetStadiumMatchs();
    }
}
