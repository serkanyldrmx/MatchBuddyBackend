using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>Turnuvalar / ligler (docs/tournaments-api.md).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentService _tournamentService;

        public TournamentsController(ITournamentService tournamentService)
        {
            _tournamentService = tournamentService;
        }

        // ?status= boş = hepsi
        [HttpGet("List")]
        public ActionResult<List<TournamentSummaryDto>> List([FromQuery] int? status)
        {
            return _tournamentService.List(User.GetPlayerId(), status);
        }

        [HttpGet("Detail")]
        public IActionResult Detail([FromQuery] int tournamentId)
        {
            var result = _tournamentService.Detail(User.GetPlayerId(), User.IsAdmin(), tournamentId);
            return this.ToHttp(result, result.Success ? result.Data : null);
        }

        [HttpPost("Create")]
        public IActionResult Create(CreateTournamentModel model)
        {
            var result = _tournamentService.Create(User.GetPlayerId(), new TournamentCreateInput
            {
                Name = model.Name,
                Description = model.Description,
                Format = model.Format,
                StartDate = model.StartDate,
                MaxTeams = model.MaxTeams,
                StadiumId = model.StadiumId,
            });
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, TournamentId = result.Data }
                : null);
        }

        [HttpPost("RegisterTeam")]
        public IActionResult RegisterTeam(TournamentTeamModel model) =>
            this.ToHttp(_tournamentService.RegisterTeam(User.GetPlayerId(), model.TournamentId, model.TeamId));

        [HttpPost("WithdrawTeam")]
        public IActionResult WithdrawTeam(TournamentTeamModel model) =>
            this.ToHttp(_tournamentService.WithdrawTeam(User.GetPlayerId(), User.IsAdmin(), model.TournamentId, model.TeamId));

        [HttpPost("Start")]
        public IActionResult Start(TournamentIdModel model) =>
            this.ToHttp(_tournamentService.Start(User.GetPlayerId(), User.IsAdmin(), model.TournamentId));

        [HttpPost("SubmitResult")]
        public IActionResult SubmitResult(SubmitFixtureResultModel model) =>
            this.ToHttp(_tournamentService.SubmitResult(User.GetPlayerId(), User.IsAdmin(),
                model.FixtureId, model.HomeScore, model.AwayScore, model.WinnerTeamId));

        [HttpPost("UpdateFixtureDate")]
        public IActionResult UpdateFixtureDate(UpdateFixtureDateModel model) =>
            this.ToHttp(_tournamentService.UpdateFixtureDate(User.GetPlayerId(), User.IsAdmin(), model.FixtureId, model.MatchDate));

        [HttpPost("Delete")]
        public IActionResult Delete(TournamentIdModel model) =>
            this.ToHttp(_tournamentService.Delete(User.GetPlayerId(), User.IsAdmin(), model.TournamentId));
    }
}
