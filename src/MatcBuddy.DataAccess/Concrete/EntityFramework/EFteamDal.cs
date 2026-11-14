using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFteamDal : EfEntityRepositoryBase<Team, MatcBuddyContext>, ITeamDal
    {
        public List<GetTeamAndPlayer> GetTeamAndPlayer()
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                var result = from t in context.Teams
                             join pt in context.PlayerTeam on t.TeamId equals pt.TeamId into teamPlayers
                             from tp in teamPlayers.DefaultIfEmpty()
                             join p in context.Players on tp.PlayerId equals p.PlayerId into playerGroup
                             from pg in playerGroup.DefaultIfEmpty()
                             group pg by new { t.TeamId, t.TeamName, t.CreatedByPlayerId } into teamGroup
                             select new GetTeamAndPlayer
                             {
                                 TeamId = teamGroup.Key.TeamId,
                                 TeamName = teamGroup.Key.TeamName,
                                 CreatedByPlayerId = teamGroup.Key.CreatedByPlayerId,
                                 PlayerName = teamGroup.Where(x => x != null).Select(x => x.UserName).ToList(),
                                 PlayerId = teamGroup.Where(x => x != null).Select(x => x.PlayerId).ToList(),
                                 ProfilePictureUrl = teamGroup.Where(x => x != null).Select(x => x.ProfilePictureUrl).ToList()
                             };

                return result.ToList();
            }
        }

        public void AddWithActivity(Team team, int creatorPlayerId)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            team.CreatedByPlayerId = creatorPlayerId;
            context.Teams.Add(team);
            context.SaveChanges();
            ParticipationCore.AddActivity(context, ActivityType.TeamCreated, creatorPlayerId, teamId: team.TeamId);
            context.SaveChanges();
            transaction.Commit();
        }

        public void ClearMatchReferences(int teamId)
        {
            using var context = new MatcBuddyContext();
            context.Matchs.Where(m => m.HomeTeamId == teamId).ExecuteUpdate(s => s.SetProperty(m => m.HomeTeamId, (int?)null));
            context.Matchs.Where(m => m.AwayTeamId == teamId).ExecuteUpdate(s => s.SetProperty(m => m.AwayTeamId, (int?)null));
        }

        public int? GetCreatorId(int teamId)
        {
            using var context = new MatcBuddyContext();
            return context.Teams.Where(t => t.TeamId == teamId).Select(t => t.CreatedByPlayerId).FirstOrDefault();
        }

        public bool Leave(int teamId, int playerId)
        {
            using var context = new MatcBuddyContext();
            var mine = context.PlayerTeam.FirstOrDefault(pt => pt.TeamId == teamId && pt.PlayerId == playerId);
            if (mine == null) return true;
            // Kalacak en eski üye (kuruculuk ona geçer)
            var next = context.PlayerTeam
                .Where(pt => pt.TeamId == teamId && pt.PlayerId != playerId)
                .OrderBy(pt => pt.PlayerTeamId)
                .Select(pt => (int?)pt.PlayerId)
                .FirstOrDefault();
            if (next == null) return false;

            using var transaction = context.Database.BeginTransaction();
            context.PlayerTeam.Remove(mine);
            var team = context.Teams.First(t => t.TeamId == teamId);
            if (team.CreatedByPlayerId == playerId) team.CreatedByPlayerId = next;
            context.SaveChanges();
            transaction.Commit();
            return true;
        }

        public bool IsInAnyTournament(int teamId)
        {
            using var context = new MatcBuddyContext();
            return context.TournamentTeams.Any(x => x.TeamId == teamId);
        }

        public bool IsPlayerInTeam(int teamId, int playerId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                return context.PlayerTeam.Any(pt => pt.TeamId == teamId && pt.PlayerId == playerId);
            }
        }

        public void SaveTeamByPlayerId(List<int> playerIds, int teamId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
            {
                // Takımın mevcut oyuncularını al
                var existingPlayerIds = context.PlayerTeam
                    .Where(pt => pt.TeamId == teamId)
                    .Select(pt => pt.PlayerId)
                    .ToList();

                // Kurucu kadrodan çıkarılamaz (ayrılmak için LeaveTeam)
                var creatorId = context.Teams.Where(t => t.TeamId == teamId).Select(t => t.CreatedByPlayerId).FirstOrDefault();
                if (creatorId != null && existingPlayerIds.Contains(creatorId.Value) && !playerIds.Contains(creatorId.Value))
                {
                    playerIds = playerIds.Append(creatorId.Value).ToList();
                }

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
