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
        public const int MinTeams = 4;
        public const int MaxTeams = 16;
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
            if (input == null) return OperationResult<int>.BadRequest("Turnuva bilgileri boş olamaz.");

            input.Name = input.Name?.Trim();
            if (string.IsNullOrEmpty(input.Name))
                return OperationResult<int>.BadRequest("Turnuva adı boş olamaz.");
            if (input.Name.Length > MaxNameLength)
                return OperationResult<int>.BadRequest($"Turnuva adı en fazla {MaxNameLength} karakter olabilir.");

            input.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            if (input.Description != null && input.Description.Length > MaxDescriptionLength)
                return OperationResult<int>.BadRequest($"Açıklama en fazla {MaxDescriptionLength} karakter olabilir.");

            if (input.Format != TournamentFormat.League && input.Format != TournamentFormat.Knockout)
                return OperationResult<int>.BadRequest("format 1 (lig) veya 2 (eleme) olmalıdır.");
            if (input.MaxTeams < MinTeams || input.MaxTeams > MaxTeams)
                return OperationResult<int>.BadRequest($"Takım sayısı {MinTeams}–{MaxTeams} arasında olmalıdır.");
            if (input.StartDate == null)
                return OperationResult<int>.BadRequest("Başlangıç tarihi gerekli.");
            if (input.StartDate.Value < DateTime.Now)
                return OperationResult<int>.BadRequest("Başlangıç tarihi geçmişte olamaz.");

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
                return OperationResult<bool>.BadRequest("İki takımın skoru da girilmeli.");
            if (hs < TournamentScheduler.MinScore || hs > TournamentScheduler.MaxScore
                || @as < TournamentScheduler.MinScore || @as > TournamentScheduler.MaxScore)
                return OperationResult<bool>.BadRequest($"Skor {TournamentScheduler.MinScore}–{TournamentScheduler.MaxScore} arasında olmalıdır.");
            return _tournamentDal.SubmitResult(me, isAdmin, fixtureId, hs, @as, winnerTeamId);
        }

        public OperationResult<bool> UpdateFixtureDate(int me, bool isAdmin, int fixtureId, DateTime? matchDate)
        {
            if (matchDate == null) return OperationResult<bool>.BadRequest("Maç tarihi gerekli.");
            return _tournamentDal.UpdateFixtureDate(me, isAdmin, fixtureId, matchDate.Value);
        }

        public OperationResult<bool> Delete(int me, bool isAdmin, int tournamentId) =>
            _tournamentDal.Delete(me, isAdmin, tournamentId);
    }
}
