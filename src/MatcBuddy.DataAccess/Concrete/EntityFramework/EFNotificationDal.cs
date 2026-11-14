using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFNotificationDal : EfEntityRepositoryBase<Notification, MatcBuddyContext>, INotificationDal
    {
        public int SendToPlayers(NotificationType type, string text, int? matchId, int? stadiumId, List<int> playerIds)
        {
            using var context = new MatcBuddyContext();
            var notification = NotificationWriter.Add(context, type, text, matchId, stadiumId, playerIds);
            if (notification == null) return 0;
            context.SaveChanges();
            return notification.PlayerNotifications.Count;
        }
    }
}
