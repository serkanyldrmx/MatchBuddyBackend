using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.DataAccess.Abstract
{
    /// <summary>Turnuvalar / ligler (docs/tournaments-api.md).</summary>
    public interface ITournamentDal
    {
        List<TournamentSummaryDto> List(int me, byte? status);
        OperationResult<TournamentDetailDto> Detail(int me, bool isAdmin, int tournamentId);
        OperationResult<int> Create(int me, TournamentCreateInput input);
        OperationResult<bool> RegisterTeam(int me, int tournamentId, int teamId);
        OperationResult<bool> WithdrawTeam(int me, bool isAdmin, int tournamentId, int teamId);
        OperationResult<bool> Start(int me, bool isAdmin, int tournamentId);
        OperationResult<bool> SubmitResult(int me, bool isAdmin, int fixtureId, int homeScore, int awayScore, int? winnerTeamId);
        OperationResult<bool> UpdateFixtureDate(int me, bool isAdmin, int fixtureId, DateTime matchDate);
        OperationResult<bool> Delete(int me, bool isAdmin, int tournamentId);
    }
}
