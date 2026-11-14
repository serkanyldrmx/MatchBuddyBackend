using System.Security.Cryptography;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Security;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    public class SocialManager : ISocialService
    {
        public static readonly string[] ReportReasons = { "spam", "hakaret", "sahte-hesap", "maca-gelmeme", "diger" };
        private const int MaxDetailsLength = 500;

        private readonly ISocialDal _socialDal;
        private readonly IPlayerDal _playerDal;
        private readonly IMatchParticipationDal _participationDal;

        public SocialManager(ISocialDal socialDal, IPlayerDal playerDal, IMatchParticipationDal participationDal)
        {
            _socialDal = socialDal;
            _playerDal = playerDal;
            _participationDal = participationDal;
        }

        public OperationResult<bool> Follow(int me, int targetPlayerId) => _socialDal.Follow(me, targetPlayerId);
        public OperationResult<bool> Unfollow(int me, int targetPlayerId) => _socialDal.Unfollow(me, targetPlayerId);

        public OperationResult<List<SocialPlayerDto>> GetFollowers(int me, int? playerId) =>
            _socialDal.GetFollowers(me, playerId ?? me);

        public OperationResult<List<SocialPlayerDto>> GetFollowing(int me, int? playerId) =>
            _socialDal.GetFollowing(me, playerId ?? me);

        public List<SocialPlayerDto> Discover(int me, string? query, int take) =>
            _socialDal.Discover(me, query, Math.Clamp(take, 1, P.Int(ParameterKeys.Social.FeedMaxTake)));

        public List<SocialPlayerDto> Suggestions(int me, int take) =>
            _socialDal.Suggestions(me, Math.Clamp(take, 1, 50));

        public OperationResult<PlayerProfileDto> GetProfile(int me, int? playerId)
        {
            var result = _socialDal.GetProfile(me, playerId ?? me);
            if (result.Success && result.Data.StatsVisible)
            {
                result.Data.Stats = _participationDal.GetPlayerStats(result.Data.Player.PlayerId);
            }
            return result;
        }

        public OperationResult<bool> Block(int me, int targetPlayerId) => _socialDal.Block(me, targetPlayerId);
        public OperationResult<bool> Unblock(int me, int targetPlayerId) => _socialDal.Unblock(me, targetPlayerId);
        public List<PlayerSummaryDto> GetBlocked(int me) => _socialDal.GetBlocked(me);

        public OperationResult<bool> Report(int me, int targetPlayerId, string? reason, string? details)
        {
            reason = reason?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(reason) || !ReportReasons.Contains(reason))
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Social.InvalidReportReason, string.Join(", ", ReportReasons));
            }
            details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
            if (details != null && details.Length > MaxDetailsLength)
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Validation.TooLong, Msg.Get(MessageKeys.Social.LabelReportDetails), MaxDetailsLength);
            }
            return _socialDal.Report(me, targetPlayerId, reason, details);
        }

        public PlayerSettingsDto GetSettings(int me)
        {
            var dto = ToDto(_socialDal.GetSettings(me));
            dto.MarketingConsent = _playerDal.Get(p => p.PlayerId == me)?.MarketingConsent ?? false;
            return dto;
        }

        public OperationResult<bool> SaveSettings(int me, PlayerSettingsInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Social.SettingsRequired);
            if (input.MessagePermission.HasValue && (input.MessagePermission < 0 || input.MessagePermission > 2))
                return OperationResult<bool>.BadRequest(ErrorCodes.Social.InvalidSettingChoice, "messagePermission");
            if (input.ProfileVisibility.HasValue && (input.ProfileVisibility < 0 || input.ProfileVisibility > 2))
                return OperationResult<bool>.BadRequest(ErrorCodes.Social.InvalidSettingChoice, "profileVisibility");
            if (input.PhoneVisibility.HasValue && (input.PhoneVisibility < 0 || input.PhoneVisibility > 2))
                return OperationResult<bool>.BadRequest(ErrorCodes.Social.InvalidSettingChoice, "phoneVisibility");
            var theme = input.Theme?.Trim().ToLowerInvariant();
            if (theme != null && !PlayerThemes.All.Contains(theme))
                return OperationResult<bool>.FieldError("theme", ErrorCodes.Social.InvalidTheme, string.Join(", ", PlayerThemes.All));
            var language = input.Language?.Trim().ToLowerInvariant();
            if (language != null && !Msg.SupportedLanguages.Contains(language))
                return OperationResult<bool>.FieldError("language", ErrorCodes.Social.InvalidLanguage, string.Join(", ", Msg.SupportedLanguages));

            // Gönderilmeyen alanlar mevcut değerini korur
            var s = _socialDal.GetSettings(me);
            s.PlayerId = me;
            if (input.MessagePermission.HasValue) s.MessagePermission = (byte)input.MessagePermission.Value;
            if (input.ProfileVisibility.HasValue) s.ProfileVisibility = (byte)input.ProfileVisibility.Value;
            if (input.NotifyFollow.HasValue) s.NotifyFollow = input.NotifyFollow.Value;
            if (input.NotifyMatchInvite.HasValue) s.NotifyMatchInvite = input.NotifyMatchInvite.Value;
            if (input.NotifyTeamInvite.HasValue) s.NotifyTeamInvite = input.NotifyTeamInvite.Value;
            if (input.NotifyMatchReminder.HasValue) s.NotifyMatchReminder = input.NotifyMatchReminder.Value;
            if (input.NotifyMatchResult.HasValue) s.NotifyMatchResult = input.NotifyMatchResult.Value;
            if (input.NotifyMatchUpdates.HasValue) s.NotifyMatchUpdates = input.NotifyMatchUpdates.Value;
            if (input.NotifyDeals.HasValue) s.NotifyDeals = input.NotifyDeals.Value;
            if (input.PhoneVisibility.HasValue) s.PhoneVisibility = (byte)input.PhoneVisibility.Value;
            if (theme != null) s.Theme = theme;
            if (language != null) s.Language = language;

            _socialDal.SaveSettings(s);
            // Kampanya / duyuru e-postası izni oyuncu kaydında tutulur (KVKK onayıyla aynı alan)
            if (input.MarketingConsent.HasValue)
            {
                var player = _playerDal.Get(p => p.PlayerId == me);
                if (player != null && player.MarketingConsent != input.MarketingConsent.Value)
                {
                    player.MarketingConsent = input.MarketingConsent.Value;
                    player.MarketingConsentAt = DateTime.UtcNow;
                    _playerDal.Update(player);
                }
            }
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.SettingsSaved));
        }

        private static PlayerSettingsDto ToDto(PlayerSettings s) => new()
        {
            MessagePermission = s.MessagePermission,
            ProfileVisibility = s.ProfileVisibility,
            NotifyFollow = s.NotifyFollow,
            NotifyMatchInvite = s.NotifyMatchInvite,
            NotifyTeamInvite = s.NotifyTeamInvite,
            NotifyMatchReminder = s.NotifyMatchReminder,
            NotifyMatchResult = s.NotifyMatchResult,
            NotifyMatchUpdates = s.NotifyMatchUpdates,
            NotifyDeals = s.NotifyDeals,
            PhoneVisibility = s.PhoneVisibility,
            Theme = s.Theme,
            Language = s.Language,
        };

        public OperationResult<bool> DeleteAccount(int me, string? password)
        {
            var player = _playerDal.Get(p => p.PlayerId == me);
            if (player == null || player.IsDeleted)
                return OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
            // Sosyal girişle açılmış ve şifre belirlenmemiş hesapta şifre sorulmaz (docs/auth-api.md)
            if (player.HasPassword)
            {
                if (string.IsNullOrEmpty(password))
                    return OperationResult<bool>.BadRequest(ErrorCodes.Auth.PasswordRequired);
                if (!PasswordHasher.Verify(password, player.Password, out _))
                    return OperationResult<bool>.BadRequest(ErrorCodes.Auth.PasswordWrong);
            }

            // Eski şifre ile giriş yapılamasın: rastgele bir değerin hash'i
            var randomHash = PasswordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            return _socialDal.DeleteAccount(me, randomHash)
                ? OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.AccountDeleted))
                : OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
        }

        public OperationResult<bool> CheckCanMessage(int senderId, int recipientId) =>
            _socialDal.CheckCanMessage(senderId, recipientId);

        public List<MessageContactDto> GetMessageContacts(int me) => _socialDal.GetMessageContacts(me);

        public List<ActivityDto> GetFeed(int me, int take, DateTime? before) =>
            _socialDal.GetFeed(me, Math.Clamp(take, 1, P.Int(ParameterKeys.Social.FeedMaxTake)), before);

        public List<Player> GetVisiblePlayers(int viewerPlayerId) => _socialDal.GetVisiblePlayers(viewerPlayerId);
        public HashSet<int> OwnerIds(IEnumerable<int> playerIds) => _socialDal.OwnerIds(playerIds);

        public List<AdminReportDto> GetReports(byte? status) => _socialDal.GetReports(status);
        public OperationResult<bool> ResolveReport(int reportId) => _socialDal.ResolveReport(reportId);
    }

    public class InviteManager : IInviteService
    {
        private static int MaxInviteBatch => P.Int(ParameterKeys.Social.MaxInviteBatch);
        private readonly IInviteDal _inviteDal;

        public InviteManager(IInviteDal inviteDal)
        {
            _inviteDal = inviteDal;
        }

        public OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int>? playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.NoPlayers);
            if (playerIds.Count > MaxInviteBatch)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.BatchLimit, MaxInviteBatch);
            return _inviteDal.InviteToMatch(me, isAdmin, matchId, playerIds);
        }

        public OperationResult<InviteResultDto> CallSubstitutes(int me, bool isAdmin, int matchId, List<int>? playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.NoPlayers);
            if (playerIds.Count > MaxInviteBatch)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.BatchLimit, MaxInviteBatch);
            return _inviteDal.InviteToMatch(me, isAdmin, matchId, playerIds, substitute: true);
        }

        public OperationResult<SubstituteCandidatesDto> GetSubstituteCandidates(int me, bool isAdmin, int matchId) =>
            _inviteDal.GetSubstituteCandidates(me, isAdmin, matchId);

        public OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int>? playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.NoPlayers);
            if (playerIds.Count > MaxInviteBatch)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.BatchLimit, MaxInviteBatch);
            return _inviteDal.InviteToTeam(me, teamId, playerIds);
        }

        public MyInvitesDto GetMyInvites(int me) => _inviteDal.GetMyInvites(me);
        public int GetPendingCount(int me) => _inviteDal.GetPendingCount(me);

        public OperationResult<byte?> Respond(int me, string? kind, int inviteId, bool accept)
        {
            return kind?.Trim().ToLowerInvariant() switch
            {
                "match" => _inviteDal.RespondMatch(me, inviteId, accept),
                "team" => _inviteDal.RespondTeam(me, inviteId, accept),
                _ => OperationResult<byte?>.BadRequest(ErrorCodes.Invite.InvalidKind),
            };
        }
    }
}
