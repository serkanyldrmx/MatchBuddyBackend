using MatcBuddy.Business.Abstract;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
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

        public OperationResult<byte> Join(int matchId, int playerId, byte? side = null, int? slot = null) =>
            _participationDal.Join(matchId, playerId, side, slot);

        public OperationResult<LeaveResultDto> Leave(int matchId, int playerId, string? reasonCode = null, string? note = null)
        {
            reasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim().ToLowerInvariant();
            if (reasonCode != null && !ExcuseReason.All.Contains(reasonCode))
                return OperationResult<LeaveResultDto>.FieldError("reasonCode", ErrorCodes.Reliability.InvalidReason);
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note != null && note.Length > ExcuseReason.NoteMaxLength)
                return OperationResult<LeaveResultDto>.FieldError("note", ErrorCodes.Reliability.NoteTooLong, ExcuseReason.NoteMaxLength);
            return _participationDal.Leave(matchId, playerId, reasonCode, note);
        }

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

        /// <summary>Taraf başına en yüksek slot numarası (büyük kadrolar dahil güvenli üst sınır).</summary>
        private const int MaxLineupSlot = 49;

        public OperationResult<bool> ArrangeLineup(int matchId, int actorPlayerId, bool isAdmin, List<LineupItemInput>? items)
        {
            if (items == null || items.Count == 0 || items.Count > 2 * (MaxLineupSlot + 1))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.InvalidLineup);
            foreach (var item in items)
            {
                if (item.Side != ParticipantSide.Home && item.Side != ParticipantSide.Away)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Match.InvalidSide);
                if (item.PlayerId.HasValue == item.GuestId.HasValue || item.Slot < 0 || item.Slot > MaxLineupSlot)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Match.InvalidLineup);
            }
            // Aynı kişi iki kez ya da aynı tarafta aynı yere iki kişi olamaz
            if (items.Where(i => i.PlayerId.HasValue).GroupBy(i => i.PlayerId).Any(g => g.Count() > 1)
                || items.Where(i => i.GuestId.HasValue).GroupBy(i => i.GuestId).Any(g => g.Count() > 1)
                || items.GroupBy(i => new { i.Side, i.Slot }).Any(g => g.Count() > 1))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.InvalidLineup);

            return _participationDal.ArrangeLineup(matchId, actorPlayerId, isAdmin, items);
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

        public OperationResult<bool> UpdateInfo(int actorPlayerId, bool isAdmin, UpdateMatchInfoInput input) =>
            _participationDal.UpdateInfo(actorPlayerId, isAdmin, input);

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

        public IResult DeleteMatch(int matchId, int? actorPlayerId = null)
        {
            return _participationDal.DeleteMatch(matchId, actorPlayerId)
                ? new SuccessResult(Msg.Get(MessageKeys.Match.Deleted))
                : new ErrorResult(ErrorCodes.Match.NotFound);
        }

        public int SendDueReminders() => _participationDal.SendDueReminders();
    }
}
