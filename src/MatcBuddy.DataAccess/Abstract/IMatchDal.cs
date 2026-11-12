using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IMatchDal : IEntityRepository<Match>
    {
        List<MatchComentsDto> GetMatchComents(int matchId);
        List<MatchTeamDto> GetMatchTeam(int matchId);
        void MatchLiked(int matchId);
        void MatchStatusUpdate(int matchId, byte status);
        List<Match> GetMatchToDate(Match match);
        void AddNotificationToMatch(int matchId, int teamId);
        void NotificationMatchStatusUpdate(int matchId, byte status);
        bool IsPlayerInMatch(int matchId, int playerId);
    }
}
