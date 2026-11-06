using MatchBuddy.Business.Abstract;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class MatchParticipationManager : IMatchParticipationService
    {
        private const int MaxScore = 50;
        private const int MaxPlayerStat = 30;

        private readonly IMatchParticipationDal _participationDal;

        public MatchParticipationManager(IMatchParticipationDal participationDal)
        {
            _participationDal = participationDal;
        }

        public OperationResult<bool> CheckCanManage(int matchId, int playerId, bool isAdmin) =>
            _participationDal.CheckCanManage(matchId, playerId, isAdmin);

        public OperationResult<byte> Join(int matchId, int playerId) =>
            _participationDal.Join(matchId, playerId);

        public OperationResult<bool> Leave(int matchId, int playerId) =>
            _participationDal.Leave(matchId, playerId);

        public OperationResult<bool> Confirm(int matchId, int playerId) =>
            _participationDal.Confirm(matchId, playerId);

        public OperationResult<bool> SetSide(int matchId, int actorPlayerId, bool isAdmin, int targetPlayerId, byte side)
        {
            if (side != ParticipantSide.Home && side != ParticipantSide.Away)
            {
                return OperationResult<bool>.BadRequest("Taraf 1 (ev sahibi) veya 2 (deplasman) olmalıdır.");
            }
            return _participationDal.SetSide(matchId, actorPlayerId, isAdmin, targetPlayerId, side);
        }

        public OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput>? stats)
        {
            if (homeScore < 0 || homeScore > MaxScore || awayScore < 0 || awayScore > MaxScore)
            {
                return OperationResult<bool>.BadRequest($"Skorlar 0 ile {MaxScore} arasında olmalıdır.");
            }
            stats ??= new List<PlayerStatInput>();
            if (stats.Any(s => s.Goals < 0 || s.Goals > MaxPlayerStat || s.Assists < 0 || s.Assists > MaxPlayerStat))
            {
                return OperationResult<bool>.BadRequest($"Gol ve asist sayıları 0 ile {MaxPlayerStat} arasında olmalıdır.");
            }
            return _participationDal.SubmitResult(matchId, actorPlayerId, isAdmin, homeScore, awayScore, stats);
        }

        public OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId) =>
            _participationDal.VoteMvp(matchId, voterPlayerId, votedPlayerId);

        public OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput>? ratings)
        {
            if (ratings == null || ratings.Count == 0)
            {
                return OperationResult<bool>.BadRequest("En az bir oyuncuya puan vermelisin.");
            }
            if (ratings.Any(r => r.Score < 1 || r.Score > 5))
            {
                return OperationResult<bool>.BadRequest("Puan 1 ile 5 arasında olmalıdır.");
            }
            return _participationDal.RatePlayers(matchId, raterPlayerId, ratings);
        }

        public OperationResult<ToggleLikeResultDto> ToggleLike(int matchId, int playerId) =>
            _participationDal.ToggleLike(matchId, playerId, likeOnly: false);

        public OperationResult<ToggleLikeResultDto> Like(int matchId, int playerId) =>
            _participationDal.ToggleLike(matchId, playerId, likeOnly: true);

        public void FillFromWaitlist(int matchId) => _participationDal.FillFromWaitlist(matchId);

        public IDataResult<List<MatchFeedItemDto>> GetMatchFeed(int playerId) =>
            new SuccessDataResult<List<MatchFeedItemDto>>(_participationDal.GetMatchFeed(playerId));

        public IDataResult<MatchDetailDto?> GetMatchDetail(int matchId, int playerId, bool isAdmin)
        {
            var detail = _participationDal.GetMatchDetail(matchId, playerId, isAdmin);
            return detail == null
                ? new ErrorDataResult<MatchDetailDto?>("Maç bulunamadı.")
                : new SuccessDataResult<MatchDetailDto?>(detail);
        }

        public IDataResult<PlayerStatsDto?> GetPlayerStats(int playerId)
        {
            var stats = _participationDal.GetPlayerStats(playerId);
            return stats == null
                ? new ErrorDataResult<PlayerStatsDto?>("Oyuncu bulunamadı.")
                : new SuccessDataResult<PlayerStatsDto?>(stats);
        }

        public IDataResult<List<LeaderboardRowDto>> GetLeaderboard(int top, int viewerPlayerId)
        {
            top = Math.Clamp(top, 1, 100);
            return new SuccessDataResult<List<LeaderboardRowDto>>(_participationDal.GetLeaderboard(top, viewerPlayerId));
        }

        // Varsayılan aralık: bu ayın başından 2 ay sonrasına kadar
        public IDataResult<List<CalendarItemDto>> GetMyCalendar(int playerId, DateTime? from, DateTime? to)
        {
            var start = from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = to ?? start.AddMonths(2);
            if (end < start)
            {
                return new ErrorDataResult<List<CalendarItemDto>>("Bitiş tarihi başlangıçtan önce olamaz.");
            }
            return new SuccessDataResult<List<CalendarItemDto>>(_participationDal.GetMyCalendar(playerId, start, end));
        }

        public IResult DeleteMatch(int matchId)
        {
            return _participationDal.DeleteMatch(matchId)
                ? new SuccessResult("Maç silindi.")
                : new ErrorResult("Maç bulunamadı.");
        }

        public int SendDueReminders() => _participationDal.SendDueReminders();
    }
}
