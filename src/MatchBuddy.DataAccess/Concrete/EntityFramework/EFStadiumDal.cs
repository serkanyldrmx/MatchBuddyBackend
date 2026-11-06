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
                var notificationText = Msg.GetDefault(MessageKeys.Venue.NotifStadiumAdded, stadium.StadiumName, stadium.Address);

                // Tüm (silinmemiş) oyunculara bildirim; tercihler NotificationWriter'da kontrol edilir
                var playerIds = context.Players.Where(p => !p.IsDeleted).Select(p => p.PlayerId).ToList();
                NotificationWriter.Add(context, NotificationType.StadiumAdded, notificationText, null, stadium.StadiumId, playerIds);

                // Değişiklikleri kaydediyoruz
                context.SaveChanges();
            }
        }


    }
}
