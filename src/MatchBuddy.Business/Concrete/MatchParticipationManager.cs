using MatchBuddy.Business.Abstract;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class MatchParticipationManager : IMatchParticipationService
    {
        private static int MaxScore => P.Int(ParameterKeys.Match.MaxScore);
        private static int MaxPlayerStat => P.Int(ParameterKeys.Match.MaxPlayerStat);

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
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.InvalidSide);
            }
            return _participationDal.SetSide(matchId, actorPlayerId, isAdmin, targetPlayerId, side);
        }

        // ---- Misafir oyuncular ----

        private static AppError? ValidateGuest(byte side, ref string? name, ref string? note)
        {
            if (side != ParticipantSide.Home && side != ParticipantSide.Away)
                return AppError.Bad(ErrorCodes.Match.InvalidSide);
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length < MatchGuestRules.NameMinLength || name.Length > MatchGuestRules.NameMaxLength)
                return AppError.Bad(ErrorCodes.Match.GuestNameLength, MatchGuestRules.NameMinLength, MatchGuestRules.NameMaxLength);
            note = note?.Trim();
            if (string.IsNullOrEmpty(note)) note = null;
            else if (note.Length > MatchGuestRules.NoteMaxLength)
                return AppError.Bad(ErrorCodes.Validation.TooLong, Msg.Get(MessageKeys.Match.LabelGuestNote), MatchGuestRules.NoteMaxLength);
            return null;
        }

        public OperationResult<int> AddGuest(int matchId, int actorPlayerId, bool isAdmin, byte side, string? name, string? note)
        {
            var error = ValidateGuest(side, ref name, ref note);
            if (error != null) return OperationResult<int>.Fail(error);
            return _participationDal.AddGuest(matchId, actorPlayerId, isAdmin, side, name!, note);
        }

        public OperationResult<bool> UpdateGuest(int guestId, int actorPlayerId, bool isAdmin, string? name, string? note, byte side)
        {
            var error = ValidateGuest(side, ref name, ref note);
            if (error != null) return OperationResult<bool>.Fail(error);
            return _participationDal.UpdateGuest(guestId, actorPlayerId, isAdmin, name!, note, side);
        }

        public OperationResult<bool> RemoveGuest(int guestId, int actorPlayerId, bool isAdmin) =>
            _participationDal.RemoveGuest(guestId, actorPlayerId, isAdmin);

        public OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput>? stats)
        {
            if (homeScore < 0 || homeScore > MaxScore || awayScore < 0 || awayScore > MaxScore)
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Validation.OutOfRange, Msg.Get(MessageKeys.Match.LabelScores), 0, MaxScore);
            }
            stats ??= new List<PlayerStatInput>();
            if (stats.Any(s => s.Goals < 0 || s.Goals > MaxPlayerStat || s.Assists < 0 || s.Assists > MaxPlayerStat))
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Validation.OutOfRange, Msg.Get(MessageKeys.Match.LabelGoalsAssists), 0, MaxPlayerStat);
            }
            return _participationDal.SubmitResult(matchId, actorPlayerId, isAdmin, homeScore, awayScore, stats);
        }

        public OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId) =>
            _participationDal.VoteMvp(matchId, voterPlayerId, votedPlayerId);

        public OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput>? ratings)
        {
            if (ratings == null || ratings.Count == 0)
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RatingsRequired);
            }
            var ratingMax = P.Int(ParameterKeys.Match.RatingMax);
            if (ratings.Any(r => r.Score < 1 || r.Score > ratingMax))
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Validation.OutOfRange, Msg.Get(MessageKeys.Match.LabelRatingScore), 1, ratingMax);
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
                ? new ErrorDataResult<MatchDetailDto?>(ErrorCodes.Match.NotFound)
                : new SuccessDataResult<MatchDetailDto?>(detail);
        }

        public IDataResult<PlayerStatsDto?> GetPlayerStats(int playerId)
        {
            var stats = _participationDal.GetPlayerStats(playerId);
            return stats == null
                ? new ErrorDataResult<PlayerStatsDto?>(ErrorCodes.Player.NotFound)
                : new SuccessDataResult<PlayerStatsDto?>(stats);
        }

        public IDataResult<List<LeaderboardRowDto>> GetLeaderboard(int top, int viewerPlayerId)
        {
            top = Math.Clamp(top, 1, P.Int(ParameterKeys.Match.LeaderboardMax));
            return new SuccessDataResult<List<LeaderboardRowDto>>(_participationDal.GetLeaderboard(top, viewerPlayerId));
        }

        // Varsayılan aralık: bu ayın başından 2 ay sonrasına kadar
        public IDataResult<List<CalendarItemDto>> GetMyCalendar(int playerId, DateTime? from, DateTime? to)
        {
            var start = from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = to ?? start.AddMonths(2);
            if (end < start)
            {
                return new ErrorDataResult<List<CalendarItemDto>>(ErrorCodes.Match.CalendarInvalidRange);
            }
            return new SuccessDataResult<List<CalendarItemDto>>(_participationDal.GetMyCalendar(playerId, start, end));
        }

        public IResult DeleteMatch(int matchId)
        {
            return _participationDal.DeleteMatch(matchId)
                ? new SuccessResult(Msg.Get(MessageKeys.Match.Deleted))
                : new ErrorResult(ErrorCodes.Match.NotFound);
        }

        public int SendDueReminders() => _participationDal.SendDueReminders();
    }
}
