using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.DataAccess.Abstract
{
    /// <summary>Maç ve takım davetleri (docs/social-api.md "Davetler").</summary>
    public interface IInviteDal
    {
        OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int> playerIds);
        OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int> playerIds);
        MyInvitesDto GetMyInvites(int me);
        int GetPendingCount(int me);

        // Data = katılım durumu (maç kabulünde 1/2), diğer durumlarda null
        OperationResult<byte?> RespondMatch(int me, int inviteId, bool accept);
        OperationResult<byte?> RespondTeam(int me, int inviteId, bool accept);
    }
}
