using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Notification : IEntity
    {
        public int NotificationId { get; set; }

        // Bildirim mesajı (örnek: "Maçınız onaylandı")
        public string Text { get; set; }

        // Görüldü mü? true/false
        public bool IsRead { get; set; }

        // Oluşturulma zamanı
        public DateTime CreatedAt { get; set; }

        public NotificationType Type { get; set; }
        // Opsiyonel olarak bildirime ait veriye yönlendirme yapabiliriz
        public int? MatchId { get; set; }
        public int? StadiumId { get; set; }

        // İlişki: Bildirim birden çok kullanıcıya gönderilebilir
        public List<PlayerNotification> PlayerNotifications { get; set; }
    }
}
