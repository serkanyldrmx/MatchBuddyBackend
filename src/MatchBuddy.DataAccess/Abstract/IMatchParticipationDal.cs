using MatchBuddy.Core.DataAccess;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    /// <summary>Maç kadrosu, sonuç, MVP, puanlama, beğeni ve hatırlatma işlemleri.</summary>
    public interface IMatchParticipationDal : IEntityRepository<MatchParticipant>
    {
        // Maçı ekler ve kurucuyu Going (Side 1, Confirmed) olarak kadroya yazar. Tek transaction.
        int AddMatchWithCreator(Match match, int creatorPlayerId);

        // canManage: admin veya kurucu; kurucusu olmayan eski maçlarda admin veya Going oyuncu
        OperationResult<bool> CheckCanManage(int matchId, int playerId, bool isAdmin);

        OperationResult<byte> Join(int matchId, int playerId);
        OperationResult<bool> Leave(int matchId, int playerId);
        OperationResult<bool> Confirm(int matchId, int playerId);
        OperationResult<bool> SetSide(int matchId, int actorPlayerId, bool isAdmin, int targetPlayerId, byte side);
        OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput> stats);
        OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId);
        OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput> ratings);
        OperationResult<ToggleLikeResultDto> ToggleLike(int matchId, int playerId, bool likeOnly);

        // Kapasite arttıysa yedekleri sırayla asile yükseltir
        void FillFromWaitlist(int matchId);

        List<MatchFeedItemDto> GetMatchFeed(int playerId);
        MatchDetailDto? GetMatchDetail(int matchId, int playerId, bool isAdmin);
        PlayerStatsDto? GetPlayerStats(int playerId);
        List<LeaderboardRowDto> GetLeaderboard(int top, int viewerPlayerId);

        // Takvim: Going / Waitlist / bekleyen davetli olduğum maçlar (from <= tarih < to)
        List<CalendarItemDto> GetMyCalendar(int playerId, DateTime from, DateTime to);

        // Maçı ilişkili kayıtlarıyla siler (bildirimler dahil; yorum/kadro/davet/aktivite FK cascade)
        bool DeleteMatch(int matchId);

        // 24 saat içinde başlayacak maçlar için hatırlatma gönderir; gönderilen oyuncu sayısını döner
        int SendDueReminders();
    }
}
