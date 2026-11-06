using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

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
                                 PlayerName = teamGroup.Where(x => x != null).Select(x => x.UserName).ToList(),
                                 PlayerId = teamGroup.Where(x => x != null).Select(x => x.PlayerId).ToList(),
                                 ProfilePictureUrl = teamGroup.Where(x => x != null).Select(x => x.ProfilePictureUrl).ToList()
                             };

                return result.ToList();
            }
        }

        public void AddWithActivity(Team team, int creatorPlayerId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            context.Teams.Add(team);
            context.SaveChanges();
            ParticipationCore.AddActivity(context, ActivityType.TeamCreated, creatorPlayerId, teamId: team.TeamId);
            context.SaveChanges();
            transaction.Commit();
        }

        public void ClearMatchReferences(int teamId)
        {
            using var context = new MatchBuddyContext();
            context.Matchs.Where(m => m.HomeTeamId == teamId).ExecuteUpdate(s => s.SetProperty(m => m.HomeTeamId, (int?)null));
            context.Matchs.Where(m => m.AwayTeamId == teamId).ExecuteUpdate(s => s.SetProperty(m => m.AwayTeamId, (int?)null));
        }

        public bool IsInAnyTournament(int teamId)
        {
            using var context = new MatchBuddyContext();
            return context.TournamentTeams.Any(x => x.TeamId == teamId);
        }

        public bool IsPlayerInTeam(int teamId, int playerId)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                return context.PlayerTeam.Any(pt => pt.TeamId == teamId && pt.PlayerId == playerId);
            }
        }

        public void SaveTeamByPlayerId(List<int> playerIds, int teamId)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                // Takımın mevcut oyuncularını al
                var existingPlayerIds = context.PlayerTeam
                    .Where(pt => pt.TeamId == teamId)
                    .Select(pt => pt.PlayerId)
                    .ToList();

                // Eklenmesi gereken yeni oyuncular (yeni gelen listede olup veritabanında olmayanlar)
                var playersToAdd = playerIds.Except(existingPlayerIds).ToList();

                // Silinmesi gereken oyuncular (veritabanında olup yeni gelen listede olmayanlar)
                var playersToRemove = existingPlayerIds.Except(playerIds).ToList();

                // Ekleme işlemi
                foreach (var playerId in playersToAdd)
                {
                    context.PlayerTeam.Add(new PlayerTeam
                    {
                        PlayerId = playerId,
                        TeamId = teamId
                    });
                }

                // Silme işlemi
                foreach (var playerId in playersToRemove)
                {
                    var playerTeam = context.PlayerTeam.FirstOrDefault(pt => pt.TeamId == teamId && pt.PlayerId == playerId);
                    if (playerTeam != null)
                    {
                        context.PlayerTeam.Remove(playerTeam);
                    }
                }

                context.SaveChanges();
            }
        }



    }
}
