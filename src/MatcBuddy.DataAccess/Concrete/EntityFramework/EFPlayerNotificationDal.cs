using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFPlayerNotificationDal : EfEntityRepositoryBase<PlayerNotification, MatcBuddyContext>, IPlayerNotificationDal
    {
    }
}
