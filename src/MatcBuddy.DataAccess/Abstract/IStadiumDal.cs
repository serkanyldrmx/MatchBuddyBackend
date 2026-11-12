using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IStadiumDal : IEntityRepository<Stadium>
    {
        List<GetStadiumMatchModel> GetStadiumMatchs();
        void AddNotificationStadium(Stadium stadium);
    }
}
