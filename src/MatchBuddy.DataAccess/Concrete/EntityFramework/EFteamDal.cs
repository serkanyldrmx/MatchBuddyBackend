using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFteamDal : EfEntityRepositoryBase<Team, MatchBuddyContext>, ITeamDal
    {
        public List<GetTeamAndPlayer> GetTeamAndPlayer()
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                var result = from t in context.Teams
                             join pt in context.PlayerTeam on t.TeamId equals pt.TeamId into teamPlayers
                             from tp in teamPlayers.DefaultIfEmpty()
                             join p in context.Players on tp.PlayerId equals p.PlayerId into playerGroup
                             from pg in playerGroup.DefaultIfEmpty()
                             group pg by new { t.TeamId, t.TeamName } into teamGroup
                             select new GetTeamAndPlayer
                             {
                                 TeamId = teamGroup.Key.TeamId,
                                 TeamName = teamGroup.Key.TeamName,
                                 PlayerName = teamGroup.Where(x => x != null).Select(x => x.UserName).ToList()
                             };

                return result.ToList();
            }
        }


    }
}
