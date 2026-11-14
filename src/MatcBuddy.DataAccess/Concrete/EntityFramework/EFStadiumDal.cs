using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFStadiumDal : EfEntityRepositoryBase<Stadium, MatcBuddyContext>, IStadiumDal
    {
        public List<GetStadiumMatchModel> GetStadiumMatchs()
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                // LINQ sorgusu ile istenen yapıyı oluşturuyoruz.
                var result = from s in context.Stadiums
                             where !s.IsExternal
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
            using (MatcBuddyContext context = new MatcBuddyContext())
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
