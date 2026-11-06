using MatchBuddy.Business.Abstract;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    /// <summary>Girdi doğrulaması; iş kuralları (yetki, durum, fikstür) EFTournamentDal'da.</summary>
    public class TournamentManager : ITournamentService
    {
        public static int MinTeams => P.Int(ParameterKeys.Tournament.MinTeams);
        public static int MaxTeams => P.Int(ParameterKeys.Tournament.MaxTeams);
        private const int MaxNameLength = 80;
        private const int MaxDescriptionLength = 500;

        private readonly ITournamentDal _tournamentDal;

        public TournamentManager(ITournamentDal tournamentDal)
        {
            _tournamentDal = tournamentDal;
        }

        public List<TournamentSummaryDto> List(int me, int? status)
        {
            if (status.HasValue && (status < TournamentStatus.Registration || status > TournamentStatus.Finished))
                return new List<TournamentSummaryDto>();
            return _tournamentDal.List(me, status.HasValue ? (byte)status.Value : null);
        }

        public OperationResult<TournamentDetailDto> Detail(int me, bool isAdmin, int tournamentId) =>
            _tournamentDal.Detail(me, isAdmin, tournamentId);

        public OperationResult<int> Create(int me, TournamentCreateInput input)
        {
            if (input == null) return OperationResult<int>.BadRequest(ErrorCodes.Tournament.InputRequired);

            input.Name = input.Name?.Trim();
            if (string.IsNullOrEmpty(input.Name))
                return OperationResult<int>.BadRequest(ErrorCodes.Tournament.NameRequired);
            if (input.Name.Length > MaxNameLength)
                return OperationResult<int>.BadRequest(ErrorCodes.Validation.TooLong, Msg.Get(MessageKeys.Tournament.LabelName), MaxNameLength);

            input.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            if (input.Description != null && input.Description.Length > MaxDescriptionLength)
                return OperationResult<int>.BadRequest(ErrorCodes.Validation.TooLong, Msg.Get(MessageKeys.Tournament.LabelDescription), MaxDescriptionLength);

            if (input.Format != TournamentFormat.League && input.Format != TournamentFormat.Knockout)
                return OperationResult<int>.BadRequest(ErrorCodes.Tournament.InvalidFormat);
            if (input.MaxTeams < MinTeams || input.MaxTeams > MaxTeams)
                return OperationResult<int>.BadRequest(ErrorCodes.Tournament.TeamCountOutOfRange, MinTeams, MaxTeams);
            if (input.StartDate == null)
                return OperationResult<int>.BadRequest(ErrorCodes.Tournament.StartDateRequired);
            if (input.StartDate.Value < DateTime.Now)
                return OperationResult<int>.BadRequest(ErrorCodes.Tournament.StartDateInPast);

            return _tournamentDal.Create(me, input);
        }

        public OperationResult<bool> RegisterTeam(int me, int tournamentId, int teamId) =>
            _tournamentDal.RegisterTeam(me, tournamentId, teamId);

        public OperationResult<bool> WithdrawTeam(int me, bool isAdmin, int tournamentId, int teamId) =>
            _tournamentDal.WithdrawTeam(me, isAdmin, tournamentId, teamId);

        public OperationResult<bool> Start(int me, bool isAdmin, int tournamentId) =>
            _tournamentDal.Start(me, isAdmin, tournamentId);

        public OperationResult<bool> SubmitResult(int me, bool isAdmin, int fixtureId, int? homeScore, int? awayScore, int? winnerTeamId)
        {
            if (homeScore is not int hs || awayScore is not int @as)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.ScoresRequired);
            if (hs < TournamentScheduler.MinScore || hs > TournamentScheduler.MaxScore
                || @as < TournamentScheduler.MinScore || @as > TournamentScheduler.MaxScore)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.ScoreOutOfRange, TournamentScheduler.MinScore, TournamentScheduler.MaxScore);
            return _tournamentDal.SubmitResult(me, isAdmin, fixtureId, hs, @as, winnerTeamId);
        }

        public OperationResult<bool> UpdateFixtureDate(int me, bool isAdmin, int fixtureId, DateTime? matchDate)
        {
            if (matchDate == null) return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.MatchDateRequired);
            return _tournamentDal.UpdateFixtureDate(me, isAdmin, fixtureId, matchDate.Value);
        }

        public OperationResult<bool> Delete(int me, bool isAdmin, int tournamentId) =>
            _tournamentDal.Delete(me, isAdmin, tournamentId);
    }
}
