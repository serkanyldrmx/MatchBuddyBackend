using MatcBuddy.Core.DataAccess;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    /// <summary>Maç kadrosu, sonuç, MVP, puanlama, beğeni ve hatırlatma işlemleri.</summary>
    public interface IMatchParticipationDal : IEntityRepository<MatchParticipant>
    {
        // Maçı ekler ve kurucuyu Going (Side 1, Confirmed) olarak kadroya yazar. Tek transaction.
        int AddMatchWithCreator(Match match, int creatorPlayerId);

        // canManage: admin veya kurucu; kurucusu olmayan eski maçlarda admin veya Going oyuncu
        OperationResult<bool> CheckCanManage(int matchId, int playerId, bool isAdmin);

        OperationResult<byte> Join(int matchId, int playerId, byte? side = null, int? slot = null);
        OperationResult<LeaveResultDto> Leave(int matchId, int playerId, string? reasonCode = null, string? note = null);
        OperationResult<bool> Confirm(int matchId, int playerId);
        OperationResult<bool> SetSide(int matchId, int actorPlayerId, bool isAdmin, int targetPlayerId, byte side);
        OperationResult<bool> ArrangeLineup(int matchId, int actorPlayerId, bool isAdmin, List<LineupItemInput> items);
        OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput> stats);
        OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId);
        OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput> ratings);
        OperationResult<ToggleLikeResultDto> ToggleLike(int matchId, int playerId, bool likeOnly);

        // Misafir oyuncular (canManage; maç geçmemiş ve reddedilmemiş olmalı). Girdi iş katmanında doğrulanır.
        OperationResult<int> AddGuest(int matchId, int actorPlayerId, bool isAdmin, byte side, string name, string? note);
        OperationResult<bool> UpdateGuest(int guestId, int actorPlayerId, bool isAdmin, string name, string? note, byte side);
        // Silinen misafirin yerine yedekteki ilk oyuncu yükselir (misafirin tarafına)
        OperationResult<bool> RemoveGuest(int guestId, int actorPlayerId, bool isAdmin);

        // Kapasite arttıysa yedekleri sırayla asile yükseltir
        void FillFromWaitlist(int matchId);
        OperationResult<bool> UpdateInfo(int actorPlayerId, bool isAdmin, UpdateMatchInfoInput input);

        List<MatchFeedItemDto> GetMatchFeed(int playerId);
        MatchDetailDto? GetMatchDetail(int matchId, int playerId, bool isAdmin);
        PlayerStatsDto? GetPlayerStats(int playerId);
        List<LeaderboardRowDto> GetLeaderboard(int top, int viewerPlayerId);

        // Takvim: Going / Waitlist / bekleyen davetli olduğum maçlar (from <= tarih < to)
        List<CalendarItemDto> GetMyCalendar(int playerId, DateTime from, DateTime to);

        // Maçı ilişkili kayıtlarıyla siler (bildirimler dahil; yorum/kadro/davet/aktivite FK cascade)
        bool DeleteMatch(int matchId, int? actorPlayerId = null);

        // 24 saat içinde başlayacak maçlar için hatırlatma gönderir; gönderilen oyuncu sayısını döner
        int SendDueReminders();
    }
}
