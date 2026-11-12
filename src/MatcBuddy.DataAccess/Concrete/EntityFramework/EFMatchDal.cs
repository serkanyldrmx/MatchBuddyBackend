using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFMatchDal : EfEntityRepositoryBase<Match, MatcBuddyContext>, IMatchDal
    {
        public List<MatchTeamDto> GetMatchTeam(int matchId)
        {

            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var result = from p in context.MatchTeam
                             join pt in context.PlayerTeam on p.TeamId equals pt.TeamId
                             join t in context.Teams on p.TeamId equals t.TeamId
                             join pl in context.Players on pt.PlayerId equals pl.PlayerId
                             where p.MatchId == matchId
                             select new MatchTeamDto
                             {
                                 MatchId = p.MatchId,
                                 TeamId = p.TeamId,
                                 TeamName = t.TeamName,
                                 PlayerId=pl.PlayerId,
                                 PlayerName = pl.PlayerName,
                                 PlayerSurname = pl.PlayerSurname,
                                 UserName = pl.UserName,
                                 UserScore = pl.UserScore,
                                 ProfilePictureUrl=pl.ProfilePictureUrl

                             };
                return result.ToList();
            }

        }

        // Oyuncu, maça eklenmiş takımlardan birinde mi?
        public bool IsPlayerInMatch(int matchId, int playerId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                return (from mt in context.MatchTeam
                        join pt in context.PlayerTeam on mt.TeamId equals pt.TeamId
                        where mt.MatchId == matchId && pt.PlayerId == playerId
                        select pt.PlayerId).Any();
            }
        }

        public void MatchStatusUpdate(int matchId, byte status)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var match = context.Matchs.FirstOrDefault(p => p.MatchId == matchId);

                if (match != null)
                {
                    match.IsActive = status;

                    context.SaveChanges();
                }
            }
        }

        public List<Match> GetMatchToDate(Match match)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var matchDate = match.MatchDate?.Date; // Nullable kontrolü yapılıyor
                if (matchDate == null)
                {
                    return new List<Match>(); // Eğer matchDate null ise boş liste döndür
                }

                var result = from p in context.Stadiums
                             join pt in context.Matchs on p.StadiumId equals pt.StadiumId
                             where pt.MatchDate.HasValue &&
                                   pt.MatchDate.Value.Date == matchDate
                             select new Match
                             {
                                 MatchId = pt.MatchId,
                                 MatchDate = pt.MatchDate,
                                 IsActive = pt.IsActive,
                                 MatchName = pt.MatchName,
                             };
                return result.ToList();
            }
        }

        public void MatchLiked(int matchId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var match = context.Matchs.FirstOrDefault(m => m.MatchId == matchId);

                if (match != null)
                {
                    match.Likes += 1;
                    context.SaveChanges();
                }
            }
        }

        public List<MatchComentsDto> GetMatchComents(int matchId)
        {


            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var result = from p in context.MatchComments
                             where p.MatchId == matchId
                             select new MatchComentsDto
                             {
                                 MatchId = p.MatchId,
                                 PlayerId = p.playerId,
                                 Comment = p.Comment,
                                 // Diğer alanları buraya ekleyin
                             };
                return result.ToList();
            }
            //using (MatcBuddyContext context = new MatcBuddyContext())
            //{
            //    var result = from p in context.MatchComments
            //                 //join c in context.MatchComments
            //                 //on p.MatchId equals c.MatchId
            //                 select new MatchComentsDto
            //                 {
            //                     //MatchId = matchId,
            //                     //MatchId = c.MatchId,
            //                     //PlayerId = c.playerId,
            //                     //Comment = c.Comment,
            //                 } ;
            //    return result.ToList();
            //}
        }

        public MatchComment GetMatchComents2()
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                context.Matchs.Include(x => x.MatchComments).ToList();
                return context.MatchComments.Include(x => x.Match).ThenInclude(x => x.Stadium).FirstOrDefault();

            }
        }

        public void AddNotificationToMatch(int matchId, int teamId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                // İlgili maç için sadece belirtilen takımda yer alan oyuncuları alıyoruz
                var result = from mt in context.MatchTeam
                             join pt in context.PlayerTeam on mt.TeamId equals pt.TeamId // MatchTeam ile PlayerTeam tablosu arasında ilişki
                             join pl in context.Players on pt.PlayerId equals pl.PlayerId // PlayerTeam ile Players tablosu arasında ilişki
                             where mt.MatchId == matchId && mt.TeamId == teamId // Belirtilen maç ve takım
                             select new
                             {
                                 PlayerId = pl.PlayerId,
                                 PlayerName = pl.PlayerName,
                                 PlayerSurname = pl.PlayerSurname,
                                 TeamName = pt.Team.TeamName // Takım ismi, PlayerTeam üzerinden alınıyor
                             };

                // Maç bilgilerini almak için Match ve Stadium tablolarını kullanıyoruz
                var matchDetails = (from m in context.Matchs
                                    join s in context.Stadiums on m.StadiumId equals s.StadiumId // Maçla ilişkilendirilen stadyum
                                    where m.MatchId == matchId
                                    select new
                                    {
                                        MatchName = m.MatchName,
                                        MatchDate = m.MatchDate,
                                        StadiumName = s.StadiumName,
                                        StadiumAddress = s.Address
                                    }).FirstOrDefault();

                if (matchDetails == null)
                {
                    // Eğer maç bilgisi bulunamazsa hata fırlatıyoruz
                    throw new Exception("Maç bilgileri bulunamadı.");
                }

                // Bildirim mesajını oluşturuyoruz
                var notificationText = Msg.GetDefault(MessageKeys.Match.NotifTeamAdded, matchDetails.MatchName, matchDetails.MatchDate,
                    matchDetails.StadiumName, matchDetails.StadiumAddress, result.FirstOrDefault()?.TeamName);

                // Bildirim (alıcı tercihleri NotificationWriter'da kontrol edilir)
                NotificationWriter.Add(context, NotificationType.MatchCreated, notificationText, matchId, null,
                    result.Select(p => p.PlayerId).ToList());
                context.SaveChanges();
            }
        }

        public void NotificationMatchStatusUpdate(int matchId, byte status)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var match = context.Matchs
                    .Where(m => m.MatchId == matchId)
                    .Select(m => new
                    {
                        m.MatchName,
                        m.MatchDate,
                        StadiumName = context.Stadiums.Where(s => s.StadiumId == m.StadiumId).Select(s => s.StadiumName).FirstOrDefault(),
                        StadiumAddress = context.Stadiums.Where(s => s.StadiumId == m.StadiumId).Select(s => s.Address).FirstOrDefault()
                    })
                    .FirstOrDefault();

                if (match == null)
                {
                    return;
                }

                string notificationText = "";
                NotificationType notificationType = NotificationType.MatchApproval;
                if (status == 2)
                {
                    notificationType = NotificationType.MatchApproval;
                }
                else
                {
                    notificationType = NotificationType.MatchRejection;
                }

                if (status == 2)
                {
                    notificationText = Msg.GetDefault(MessageKeys.Match.NotifApproved, match.MatchName, match.MatchDate, match.StadiumName, match.StadiumAddress);
                }
                else if (status == 3)
                {
                    notificationText = Msg.GetDefault(MessageKeys.Match.NotifRejected, match.MatchName, match.MatchDate, match.StadiumName, match.StadiumAddress);
                }
                else
                {
                    return;
                }

                var recipientIds = (from mt in context.MatchTeam
                                    join pt in context.PlayerTeam on mt.TeamId equals pt.TeamId
                                    where mt.MatchId == matchId
                                    select pt.PlayerId).ToList();

                // Bildirim (alıcı tercihleri NotificationWriter'da kontrol edilir)
                NotificationWriter.Add(context, notificationType, notificationText, matchId, null, recipientIds);
                context.SaveChanges();
            }
        }



    }
}
