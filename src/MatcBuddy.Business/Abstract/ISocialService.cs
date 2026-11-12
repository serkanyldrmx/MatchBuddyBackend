using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    /// <summary>Sosyal paket: takip, profil, engel, şikayet, ayarlar, mesaj izni, akış, hesap silme.</summary>
    public interface ISocialService
    {
        OperationResult<bool> Follow(int me, int targetPlayerId);
        OperationResult<bool> Unfollow(int me, int targetPlayerId);
        OperationResult<List<SocialPlayerDto>> GetFollowers(int me, int? playerId);
        OperationResult<List<SocialPlayerDto>> GetFollowing(int me, int? playerId);
        List<SocialPlayerDto> Discover(int me, string? query, int take);
        List<SocialPlayerDto> Suggestions(int me, int take);
        OperationResult<PlayerProfileDto> GetProfile(int me, int? playerId);

        OperationResult<bool> Block(int me, int targetPlayerId);
        OperationResult<bool> Unblock(int me, int targetPlayerId);
        List<PlayerSummaryDto> GetBlocked(int me);
        OperationResult<bool> Report(int me, int targetPlayerId, string? reason, string? details);

        PlayerSettingsDto GetSettings(int me);
        OperationResult<bool> SaveSettings(int me, PlayerSettingsInput input);

        OperationResult<bool> DeleteAccount(int me, string? password);

        OperationResult<bool> CheckCanMessage(int senderId, int recipientId);
        List<MessageContactDto> GetMessageContacts(int me);
        List<ActivityDto> GetFeed(int me, int take, DateTime? before);

        List<Player> GetVisiblePlayers(int viewerPlayerId);
        HashSet<int> OwnerIds(IEnumerable<int> playerIds);

        List<AdminReportDto> GetReports(byte? status);
        OperationResult<bool> ResolveReport(int reportId);
    }

    /// <summary>Maç ve takım davetleri.</summary>
    public interface IInviteService
    {
        OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int>? playerIds);
        OperationResult<InviteResultDto> CallSubstitutes(int me, bool isAdmin, int matchId, List<int>? playerIds);
        OperationResult<SubstituteCandidatesDto> GetSubstituteCandidates(int me, bool isAdmin, int matchId);
        OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int>? playerIds);
        MyInvitesDto GetMyInvites(int me);
        int GetPendingCount(int me);
        OperationResult<byte?> Respond(int me, string? kind, int inviteId, bool accept);
    }
}
