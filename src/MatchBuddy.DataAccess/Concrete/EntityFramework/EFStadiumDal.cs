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

    }
}
