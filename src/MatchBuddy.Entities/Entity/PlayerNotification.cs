using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class PlayerNotification : IEntity
    {
        public int PlayerNotificationId { get; set; }
        public Notification Notification { get; set; }
        public int NotificationId { get; set; }
        public Player Player { get; set; }
        public int PlayerId { get; set; }
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }
    }
}
