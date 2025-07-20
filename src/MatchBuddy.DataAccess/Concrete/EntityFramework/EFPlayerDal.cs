using MatchBuddy.Api.Model;
using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFPlayerDal : EfEntityRepositoryBase<Player, MatchBuddyContext>, IPlayerDal
    {
        public List<AIPLayerModel> AISortByPlayer(int playerId)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                var player = context.Players.FirstOrDefault(p => p.PlayerId == playerId);

                if (player == null)
                    return new List<AIPLayerModel>();

                // Oyuncunun ait olduğu takımları alıyoruz
                var teams = (from pt in context.PlayerTeam
                             join t in context.Teams on pt.TeamId equals t.TeamId
                             where pt.PlayerId == playerId
                             select new AITeamModel
                             {
                                 TeamId = t.TeamId,
                                 TeamName = t.TeamName,
                                 PlayerById = player.PlayerId,

                                 Player = (from pt2 in context.PlayerTeam
                                           join p2 in context.Players on pt2.PlayerId equals p2.PlayerId
                                           where pt2.TeamId == t.TeamId
                                           select new Player
                                           {
                                               PlayerId = p2.PlayerId,
                                               PlayerName = p2.PlayerName,
                                               PlayerSurname = p2.PlayerSurname,
                                               UserName = p2.UserName,
                                               Email = p2.Email,
                                               PhoneNumber = p2.PhoneNumber,
                                               Address = p2.Address,
                                               Size = p2.Size,
                                               Weight = p2.Weight,
                                               Age = p2.Age,
                                               IsAdmin = p2.IsAdmin,
                                               UserScore = p2.UserScore
                                           }).ToList(),

                                 Match = (from tm in context.MatchTeam
                                          join m in context.Matchs on tm.MatchId equals m.MatchId
                                          join s in context.Stadiums on m.StadiumId equals s.StadiumId
                                          where tm.TeamId == t.TeamId
                                          select new AIMatchModel
                                          {
                                              MatchId = m.MatchId,
                                              MatchName = m.MatchName,
                                              MatchDate = m.MatchDate,
                                              UserCount = m.UserCount,
                                              Description = m.Description,
                                              IsActive = m.IsActive,
                                              StadiumId = m.StadiumId,
                                              StadiumAddress = s.Address,
                                              Location = s.Location
                                          }).ToList()
                             }).ToList();

                // Ana model olarak AIPLayerModel listesine dönüştür
                return new List<AIPLayerModel>
                {
                    new AIPLayerModel
                    {
                        Address = player.Address,
                        Size = player.Size,
                        Weight = player.Weight,
                        Age = player.Age,
                        UserScore = player.UserScore,
                        Match = teams
                    }
                };
            }
        }
    }
}
