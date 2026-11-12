using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Abstract
{
    /// <summary>Maç katılımı, sonuç, MVP oyu, puanlama ve beğeni işlemleri.</summary>
    public interface IMatchParticipationService
    {
        OperationResult<bool> CheckCanManage(int matchId, int playerId, bool isAdmin);
        OperationResult<byte> Join(int matchId, int playerId);
        OperationResult<LeaveResultDto> Leave(int matchId, int playerId, string? reasonCode = null, string? note = null);
        OperationResult<bool> Confirm(int matchId, int playerId);
        OperationResult<bool> SetSide(int matchId, int actorPlayerId, bool isAdmin, int targetPlayerId, byte side);
        OperationResult<bool> ArrangeLineup(int matchId, int actorPlayerId, bool isAdmin, List<LineupItemInput> items);
        OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput>? stats);
        OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId);
        OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput>? ratings);
        OperationResult<ToggleLikeResultDto> ToggleLike(int matchId, int playerId);
        // Eski MatchLiked uç noktası: sadece beğenir (geri uyumluluk)
        OperationResult<ToggleLikeResultDto> Like(int matchId, int playerId);
        void FillFromWaitlist(int matchId);

        // Misafir oyuncular (docs/match-participation-api.md "Ek: Misafir oyuncular")
        OperationResult<int> AddGuest(int matchId, int actorPlayerId, bool isAdmin, byte side, string? name, string? note);
        OperationResult<bool> UpdateGuest(int guestId, int actorPlayerId, bool isAdmin, string? name, string? note, byte side);
        OperationResult<bool> RemoveGuest(int guestId, int actorPlayerId, bool isAdmin);

        IDataResult<List<MatchFeedItemDto>> GetMatchFeed(int playerId);
        IDataResult<MatchDetailDto?> GetMatchDetail(int matchId, int playerId, bool isAdmin);
        IDataResult<PlayerStatsDto?> GetPlayerStats(int playerId);
        IDataResult<List<LeaderboardRowDto>> GetLeaderboard(int top, int viewerPlayerId);
        IDataResult<List<CalendarItemDto>> GetMyCalendar(int playerId, DateTime? from, DateTime? to);
        IResult DeleteMatch(int matchId, int? actorPlayerId = null);

        int SendDueReminders();
    }
}
