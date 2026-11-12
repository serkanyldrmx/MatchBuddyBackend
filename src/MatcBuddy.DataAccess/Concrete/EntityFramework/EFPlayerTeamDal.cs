using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFPlayerTeamDal: EfEntityRepositoryBase<PlayerTeam, MatcBuddyContext>, IPlayerTeamDal
    {
    }
}
