using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.DataAccess.Abstract
{
    /// <summary>Maç ve takım davetleri (docs/social-api.md "Davetler").</summary>
    public interface IInviteDal
    {
        /// <param name="substitute">Yedek çağrısı: yalnızca maçı yöneten, maçta boş yer varken; bildirim metni "yer açıldı".</param>
        OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int> playerIds, bool substitute = false);
        OperationResult<SubstituteCandidatesDto> GetSubstituteCandidates(int me, bool isAdmin, int matchId);
        OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int> playerIds);
        MyInvitesDto GetMyInvites(int me);
        int GetPendingCount(int me);

        // Data = katılım durumu (maç kabulünde 1/2), diğer durumlarda null
        OperationResult<byte?> RespondMatch(int me, int inviteId, bool accept);
        OperationResult<byte?> RespondTeam(int me, int inviteId, bool accept);
    }
}
