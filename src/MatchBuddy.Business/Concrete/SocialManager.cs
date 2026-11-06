using System.Security.Cryptography;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Security;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
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
            _socialDal.Discover(me, query, Math.Clamp(take, 1, 100));

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
                return OperationResult<bool>.BadRequest("Geçersiz şikayet nedeni. Geçerli değerler: " + string.Join(", ", ReportReasons) + ".");
            }
            details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
            if (details != null && details.Length > MaxDetailsLength)
            {
                return OperationResult<bool>.BadRequest($"Açıklama en fazla {MaxDetailsLength} karakter olabilir.");
            }
            return _socialDal.Report(me, targetPlayerId, reason, details);
        }

        public PlayerSettingsDto GetSettings(int me) => ToDto(_socialDal.GetSettings(me));

        public OperationResult<bool> SaveSettings(int me, PlayerSettingsInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest("Ayarlar boş olamaz.");
            if (input.MessagePermission.HasValue && (input.MessagePermission < 0 || input.MessagePermission > 2))
                return OperationResult<bool>.BadRequest("messagePermission 0, 1 veya 2 olmalıdır.");
            if (input.ProfileVisibility.HasValue && (input.ProfileVisibility < 0 || input.ProfileVisibility > 2))
                return OperationResult<bool>.BadRequest("profileVisibility 0, 1 veya 2 olmalıdır.");

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

            _socialDal.SaveSettings(s);
            return OperationResult<bool>.Ok(true, "Ayarlar kaydedildi.");
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
        };

        public OperationResult<bool> DeleteAccount(int me, string? password)
        {
            if (string.IsNullOrEmpty(password))
                return OperationResult<bool>.BadRequest("Şifre gerekli.");

            var player = _playerDal.Get(p => p.PlayerId == me);
            if (player == null || player.IsDeleted)
                return OperationResult<bool>.NotFound("Oyuncu bulunamadı.");
            if (!PasswordHasher.Verify(password, player.Password, out _))
                return OperationResult<bool>.BadRequest("Şifre hatalı.");

            // Eski şifre ile giriş yapılamasın: rastgele bir değerin hash'i
            var randomHash = PasswordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            return _socialDal.DeleteAccount(me, randomHash)
                ? OperationResult<bool>.Ok(true, "Hesabın silindi.")
                : OperationResult<bool>.NotFound("Oyuncu bulunamadı.");
        }

        public OperationResult<bool> CheckCanMessage(int senderId, int recipientId) =>
            _socialDal.CheckCanMessage(senderId, recipientId);

        public List<MessageContactDto> GetMessageContacts(int me) => _socialDal.GetMessageContacts(me);

        public List<ActivityDto> GetFeed(int me, int take, DateTime? before) =>
            _socialDal.GetFeed(me, Math.Clamp(take, 1, 100), before);

        public List<Player> GetVisiblePlayers(int viewerPlayerId) => _socialDal.GetVisiblePlayers(viewerPlayerId);
        public HashSet<int> OwnerIds(IEnumerable<int> playerIds) => _socialDal.OwnerIds(playerIds);

        public List<AdminReportDto> GetReports(byte? status) => _socialDal.GetReports(status);
        public OperationResult<bool> ResolveReport(int reportId) => _socialDal.ResolveReport(reportId);
    }

    public class InviteManager : IInviteService
    {
        private const int MaxInviteBatch = 50;
        private readonly IInviteDal _inviteDal;

        public InviteManager(IInviteDal inviteDal)
        {
            _inviteDal = inviteDal;
        }

        public OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int>? playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return OperationResult<InviteResultDto>.BadRequest("Davet edilecek en az bir oyuncu seçmelisin.");
            if (playerIds.Count > MaxInviteBatch)
                return OperationResult<InviteResultDto>.BadRequest($"Tek seferde en fazla {MaxInviteBatch} oyuncu davet edilebilir.");
            return _inviteDal.InviteToMatch(me, isAdmin, matchId, playerIds);
        }

        public OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int>? playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return OperationResult<InviteResultDto>.BadRequest("Davet edilecek en az bir oyuncu seçmelisin.");
            if (playerIds.Count > MaxInviteBatch)
                return OperationResult<InviteResultDto>.BadRequest($"Tek seferde en fazla {MaxInviteBatch} oyuncu davet edilebilir.");
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
                _ => OperationResult<byte?>.BadRequest("kind 'match' veya 'team' olmalıdır."),
            };
        }
    }
}
