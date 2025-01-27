using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MatchController : ControllerBase
    {
        IMatchService _matchService;
        
        public MatchController(IMatchService matchService)
        {
            _matchService = matchService;
        }

        //Maç eklemek için 
        [HttpPost("SaveMatch")]
        public IActionResult SaveMatch(MatchModel matchModel)
        {
            var match = new Match() 
            {
                MatchDate = matchModel.MatchDate,
                MatchName = matchModel.MatchName,
                Description = matchModel.Description,
                IsActive = matchModel.IsActive,
                StadiumId = matchModel.StadiumId,
                UserCount = matchModel.UserCount,
            };
            var result = _matchService.Add(match);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        //Maç güncellemek için
        [HttpPost("UpdateMatch")]
        public IActionResult UpdateMatch(MatchModel matchModel)
        {
            var match = new Match()
            {
                MatchId = matchModel.MatchId,
                MatchDate = matchModel.MatchDate,
                MatchName = matchModel.MatchName,
                Description = matchModel.Description,
                IsActive = matchModel.IsActive,
                StadiumId = matchModel.StadiumId,
                UserCount = matchModel.UserCount,
            };
            var result = _matchService.Update(match);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        //Maç silmek için
        [HttpPost("DeleteMatch")]
        public IActionResult DeleteMatch(MatchModel matchModel)
        {
            var match = new Match()
            {
                MatchId = matchModel.MatchId,
                MatchDate = matchModel.MatchDate,
                MatchName = matchModel.MatchName,
                Description = matchModel.Description,
                IsActive = matchModel.IsActive,
                StadiumId = matchModel.StadiumId,
                UserCount = matchModel.UserCount,
            };
            var result = _matchService.Delete(match);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        //Tek bir maçı getirmek için 
        [HttpGet("GetMatchById")]
        public Match GetMatchById([FromQuery]int matchId)
        {
            
            IDataResult<Match> result = _matchService.GetById(matchId);
            return result.Data;
        }

        [HttpGet("GetMatchList")]
        public List<Match> GetMatchList()
        {
            var result = _matchService.GetAll();
            return result.Data;
        }
                
        [HttpGet("GetMatchComments")]
        public List<MatchComentsDto> GetMatchComments([FromQuery] int matchId)
        {
            var result = _matchService.GetMatchComents(matchId);
            return result.Data;
        }

        //bir maçtaki takımları ve bu takımlardaki oyuncu bilgilerini getirir
        [HttpGet("GetMatchTeamInfo")]
        public List<MatchTeamDto> GetMatchTeamInfo([FromQuery] int matchId)
        {
            var result = _matchService.GetMatchTeam(matchId);
            return result.Data;
        }

        //Maçtaki Takımları kayıt eder
        [HttpPost("SaveMatchTeam")]
        public IActionResult SaveMatchTeam(MatchTeamModel matchTeamModel)
        {
            var matchTeam = new MatchTeam()
            {
               MatchId= matchTeamModel.MatchId,
               TeamId= matchTeamModel.TeamId
            };
            var result = _matchService.AddMatchTeam(matchTeam);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        //Maçın durumunu günceller
        [HttpPost("MatchStatusUpdate")]
        public IActionResult MatchStatusUpdate(MatchStatusModel matchStatusModel)
        {
            
            var result = _matchService.MatchStatusUpdate(matchStatusModel.MatchId,matchStatusModel.Status);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
