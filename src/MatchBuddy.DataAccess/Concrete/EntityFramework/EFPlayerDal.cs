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
                var result = (from player in context.Players
                              join playerTeam in context.PlayerTeam on player.PlayerId equals playerTeam.PlayerId
                              join team in context.Teams on playerTeam.TeamId equals team.TeamId
                              where player.PlayerId == playerId
                              select new AIPLayerModel
                              {
                                  Address = player.Address,
                                  Size = player.Size,
                                  Weight = player.Weight,
                                  Age = player.Age,
                                  UserScore = player.UserScore,
                                  Match = (from teamMatch in context.MatchTeam
                                           join match in context.Matchs on teamMatch.MatchId equals match.MatchId
                                           join stadium in context.Stadiums on match.StadiumId equals stadium.StadiumId
                                           where teamMatch.TeamId == team.TeamId
                                           select new AITeamModel
                                           {
                                               TeamId = team.TeamId,
                                               TeamName = team.TeamName,
                                               PlayerById = player.PlayerId,
                                               Player = (from pt in context.PlayerTeam
                                                         join p in context.Players on pt.PlayerId equals p.PlayerId
                                                         where pt.TeamId == team.TeamId
                                                         select p).ToList(),
                                               Match = (from tm in context.MatchTeam
                                                        join m in context.Matchs on tm.MatchId equals m.MatchId
                                                        join s in context.Stadiums on m.StadiumId equals s.StadiumId
                                                        where tm.TeamId == team.TeamId
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
                                           }).ToList()
                              }).ToList();

                return result;
            }

        }

    }
}
