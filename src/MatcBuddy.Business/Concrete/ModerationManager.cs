using MatcBuddy.Business.Abstract;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    /// <summary>
    /// Kullanıcı engelleme ve şikayet sonuçlandırma. Engel tarihleri UTC tutulur; süresi geçen engel kendiliğinden düşer
    /// (Player.IsBanActive). Yönetici hesapları ve yöneticinin kendisi engellenemez.
    /// </summary>
    public class ModerationManager : IModerationService
    {
        public const int MaxReasonLength = 500;
        public const int MaxPageSize = 100;

        private readonly IModerationDal _dal;
        private readonly Func<DateTime> _utcNow;

        public ModerationManager(IModerationDal dal) : this(dal, () => DateTime.UtcNow) { }

        public ModerationManager(IModerationDal dal, Func<DateTime> utcNow)
        {
            _dal = dal;
            _utcNow = utcNow;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>İstemciden gelen tarih: yerel ise UTC'ye çevrilir, türü belirsizse UTC kabul edilir.</summary>
        public static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };

        public PagedDto<AdminUserDto> SearchUsers(string? query, bool bannedOnly, int page, int pageSize) =>
            _dal.SearchUsers(Clean(query), bannedOnly, Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize), _utcNow());

        public OperationResult<bool> Ban(int adminId, BanUserInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            if (input.PlayerId == adminId) return OperationResult<bool>.BadRequest(ErrorCodes.Moderation.CannotBanSelf);

            var now = _utcNow();
            DateTime? until = input.Until.HasValue ? ToUtc(input.Until.Value) : null;
            if (until.HasValue && until.Value <= now)
                return OperationResult<bool>.FieldError("until", ErrorCodes.Moderation.BanUntilPast);
            var reason = Clean(input.Reason);
            if (reason?.Length > MaxReasonLength)
                return OperationResult<bool>.FieldError("reason", ErrorCodes.Validation.TooLong, "reason", MaxReasonLength);

            var player = _dal.FindPlayer(input.PlayerId);
            if (player == null) return OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
            if (player.Role == PlayerRoles.Admin || player.IsAdmin == 1)
                return OperationResult<bool>.BadRequest(ErrorCodes.Moderation.CannotBanAdmin);

            _dal.SetBan(player.PlayerId, true, until, reason);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Moderation.UserBanned));
        }

        public OperationResult<bool> Unban(int playerId)
        {
            var player = _dal.FindPlayer(playerId);
            if (player == null) return OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
            // Engelli değilse de başarılı (idempotent)
            if (player.IsBanned) _dal.SetBan(playerId, false, null, null);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Moderation.UserUnbanned));
        }

        public OperationResult<int?> ResolveReport(int adminId, ResolveReportInput input)
        {
            if (input == null) return OperationResult<int?>.BadRequest(ErrorCodes.Validation.BodyRequired);
            if (!ReportResolutionActions.IsValid(input.Action))
                return OperationResult<int?>.FieldError("action", ErrorCodes.Validation.InvalidValue, input.Action, "action");
            var note = Clean(input.Note);
            if (note?.Length > MaxReasonLength)
                return OperationResult<int?>.FieldError("note", ErrorCodes.Validation.TooLong, "note", MaxReasonLength);

            var report = _dal.FindReport(input.ReportId);
            if (report == null) return OperationResult<int?>.NotFound(ErrorCodes.Social.ReportNotFound);

            int? bannedPlayerId = null;
            if (input.Action == ReportResolutionActions.Ban)
            {
                var ban = Ban(adminId, new BanUserInput
                {
                    PlayerId = report.ReportedPlayerId,
                    Until = input.BanUntil,
                    Reason = Clean(input.BanReason) ?? note,
                });
                if (!ban.Success) return OperationResult<int?>.From(ban);
                bannedPlayerId = report.ReportedPlayerId;
            }

            _dal.ResolveReport(report.ReportId, adminId, note, input.Action, _utcNow());
            return OperationResult<int?>.Ok(bannedPlayerId, Msg.Get(MessageKeys.Social.ReportResolved));
        }
    }
}
