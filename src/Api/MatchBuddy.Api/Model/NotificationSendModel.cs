using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class NotificationSendModel
    {
        public List<int> PlayerIds { get; set; }  // Bildirim gönderilecek kullanıcılar
        public string Text { get; set; }          // Bildirim mesajı
        public NotificationType Type { get; set; } // Bilgilendirme, uyarı vb.
        public int? MatchId { get; set; }         // İsteğe bağlı
        public int? StadiumId { get; set; }       // İsteğe bağlı
    }
}
