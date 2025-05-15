using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFStadiumDal : EfEntityRepositoryBase<Stadium, MatchBuddyContext>, IStadiumDal
    {
        public List<GetStadiumMatchModel> GetStadiumMatchs()
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                // LINQ sorgusu ile istenen yapıyı oluşturuyoruz.
                var result = from s in context.Stadiums
                             select new GetStadiumMatchModel
                             {
                                 StadiumId = s.StadiumId,
                                 StadiumName = s.StadiumName,
                                 MatchModel = context.Matchs
                                     .Where(m => m.StadiumId == s.StadiumId && m.IsActive == 1)
                                     .Select(m => new GetMatchModel
                                     {
                                         MatchId = m.MatchId,
                                         MatchName = m.MatchName,
                                         IsActive = m.IsActive
                                     }).ToList()
                             };

                return result.ToList();
            }
        }

        public void AddNotificationStadium(Stadium stadium)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                // Yeni stadyum için bildirim metni oluşturuyoruz
                var notificationText = $"Tebrikler yeni bir stadyum ekledi: {stadium.StadiumName} - {stadium.Address}";

                // Bildirim oluşturuluyor
                var notification = new Notification
                {
                    Text = notificationText,
                    Type = NotificationType.StadiumAdded, // Bildirimin türü "StadiumAdded"
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    StadiumId = stadium.StadiumId,
                    PlayerNotifications = new List<PlayerNotification>() // Bildirim tüm kullanıcılara gönderilecek
                };

                // Bildirimi veritabanına ekliyoruz
                context.Notification.Add(notification);
                context.SaveChanges();

                // Tüm oyunculara bildirim gönderiyoruz
                var players = context.Players.ToList(); // Tüm oyuncuları alıyoruz
                foreach (var player in players)
                {
                    var playerNotification = new PlayerNotification
                    {
                        PlayerId = player.PlayerId,
                        NotificationId = notification.NotificationId // Bildirimi ilişkilendiriyoruz
                    };

                    // PlayerNotification ekliyoruz
                    context.PlayerNotification.Add(playerNotification);
                }

                // Değişiklikleri kaydediyoruz
                context.SaveChanges();
            }
        }


    }
}
