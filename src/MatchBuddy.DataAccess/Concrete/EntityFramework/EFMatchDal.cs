using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFMatchDal : EfEntityRepositoryBase<Match, MatchBuddyContext>, IMatchDal
    {
        public List<MatchTeamDto> GetMatchTeam(int matchId)
        {

            using (MatchBuddyContext context = new MatchBuddyContext())
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
                                 UserScore = pl.UserScore
                             };
                return result.ToList();
            }

        }

        public void MatchStatusUpdate(int matchId, byte status)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
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
            using (MatchBuddyContext context = new MatchBuddyContext())
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
            using (MatchBuddyContext context = new MatchBuddyContext())
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


            using (MatchBuddyContext context = new MatchBuddyContext())
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
            //using (MatchBuddyContext context = new MatchBuddyContext())
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
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                context.Matchs.Include(x => x.MatchComments).ToList();
                return context.MatchComments.Include(x => x.Match).ThenInclude(x => x.Stadium).FirstOrDefault();

            }
        }

        public void AddNotificationToMatch(int matchId, int teamId)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
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
                var notificationText = $"Tebrikler Takımınız Yeni Bir Maça Eklendi: {matchDetails.MatchName} ({matchDetails.MatchDate}) - {matchDetails.StadiumName}, {matchDetails.StadiumAddress}. {result.FirstOrDefault()?.TeamName} takımı için yeni bir maç başlatıldı.";

                // Bildirim oluşturuluyor
                var notification = new Notification
                {
                    Text = notificationText,
                    Type = NotificationType.MatchCreated, // Bildirimin türü "MatchCreated"
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    MatchId = matchId,
                    StadiumId = null, // Stadyum adı eklenebilir
                    PlayerNotifications = new List<PlayerNotification>() // Bildirim oyunculara gönderilecek
                };

                // Bildirimi veritabanına ekliyoruz
                context.Notification.Add(notification);
                context.SaveChanges();

                // Her oyuncu için PlayerNotification tablosuna kayıt ekliyoruz
                foreach (var player in result)
                {
                    var playerNotification = new PlayerNotification
                    {
                        PlayerId = player.PlayerId,
                        NotificationId = notification.NotificationId // Bildirim ID'sini ilişkilendiriyoruz
                    };

                    // PlayerNotification ekliyoruz
                    context.PlayerNotification.Add(playerNotification);
                }

                // Değişiklikleri kaydediyoruz
                context.SaveChanges();
            }
        }

        public void NotificationMatchStatusUpdate(int matchId, byte status)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
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
                    notificationText = $"Tebrikler! Maçınız onaylandı: '{match.MatchName}' maçı, {match.MatchDate?.ToString("dd MMMM yyyy")} tarihinde, {match.StadiumName} ({match.StadiumAddress}) stadyumunda gerçekleşecektir.";
                }
                else if (status == 3)
                {
                    notificationText = $"Üzgünüz! Maçınız reddedildi: '{match.MatchName}' maçı, {match.MatchDate?.ToString("dd MMMM yyyy")} tarihinde, {match.StadiumName} ({match.StadiumAddress}) stadyumunda yapılacaktı.";
                }
                else
                {
                    return;
                }

                var notification = new Notification
                {
                    Text = notificationText,
                    Type = notificationType,
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    MatchId = matchId
                };

                context.Notification.Add(notification);
                context.SaveChanges();

                var matchTeams = context.MatchTeam
                    .Where(mt => mt.MatchId == matchId)
                    .Select(mt => mt.TeamId)
                    .ToList();

                foreach (var teamId in matchTeams)
                {
                    var players = context.PlayerTeam
                        .Where(pt => pt.TeamId == teamId)
                        .Select(pt => pt.PlayerId)
                        .ToList();

                    foreach (var playerId in players)
                    {
                        var playerNotification = new PlayerNotification
                        {
                            PlayerId = playerId,
                            NotificationId = notification.NotificationId
                        };

                        context.PlayerNotification.Add(playerNotification);
                    }
                }

                context.SaveChanges();
            }
        }



    }
}
