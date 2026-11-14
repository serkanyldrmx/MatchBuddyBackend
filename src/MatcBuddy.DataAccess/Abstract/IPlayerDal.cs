using MatcBuddy.Api.Model;
using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IPlayerDal : IEntityRepository<Player>
    {
        List<AIPLayerModel> AISortByPlayer(int playerId);
    }
}
