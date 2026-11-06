using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFNotificationDal : EfEntityRepositoryBase<Notification, MatchBuddyContext>, INotificationDal
    {
        public int SendToPlayers(NotificationType type, string text, int? matchId, int? stadiumId, List<int> playerIds)
        {
            using var context = new MatchBuddyContext();
            var notification = NotificationWriter.Add(context, type, text, matchId, stadiumId, playerIds);
            if (notification == null) return 0;
            context.SaveChanges();
            return notification.PlayerNotifications.Count;
        }
    }
}
