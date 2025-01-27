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
    }
}
