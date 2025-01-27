using MatchBuddy.Api.Model;
using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IPlayerDal : IEntityRepository<Player>
    {
        List<AIPLayerModel> AISortByPlayer(int playerId);
    }
}
