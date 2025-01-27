using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamController : ControllerBase
    {
        ITeamService _teamService;
        IPlayerTeamService _playerTeamService;

        public TeamController(ITeamService teamService, IPlayerTeamService playerTeamService)
        {
            _teamService = teamService;
            _playerTeamService = playerTeamService;
        }

        [HttpPost("SaveTeam")]
        public IActionResult SaveTeam(TeamModel teamModel)
        {
            var team = new Team
            {
                TeamName = teamModel.TeamName,
            };
            var result = _teamService.Add(team);
            if (result.Success)
            {
                foreach (var playerId in teamModel.PlayerId)
                {
                    var playerTeam = new PlayerTeam()
                    {
                        PlayerId = playerId,
                        TeamId = team.TeamId // group nesnesinin Id'sini kullanarak gruba ait olduğu belirtiliyor
                    };

                    var result1 = _playerTeamService.Add(playerTeam);

                    if (!result1.Success)
                    {
                        return BadRequest(result1); // Eğer ekleme işlemi başarısız olursa, döngüyü sonlandırıp hata döndürüyoruz
                    }
                }

                return Ok(result);
            }
            return BadRequest(result);
        }

        //[HttpPost("SavePlayerTeam")]
        //public IActionResult SavePlayerTeam(TeamModel teamModel)
        //{
        //    var playerTeam = new PlayerTeam()
        //    {
        //        PlayerId = teamModel.PlayerById,
        //        TeamId = teamModel.TeamId // group nesnesinin Id'sini kullanarak gruba ait olduğu belirtiliyor
        //    };

        //    var result1 = _playerTeamService.Add(playerTeam);

        //    if (!result1.Success)
        //    {
        //        return BadRequest(result1); // Eğer ekleme işlemi başarısız olursa, döngüyü sonlandırıp hata döndürüyoruz
        //    }
        //    return Ok(result1);
        //    return BadRequest(result1);
        //}

        [HttpGet("GetTeamList")]
        public List<GetTeamAndPlayer> GetTeamList()
        {
            ITeamService teamService = new TeamManager(new EFteamDal());
            var result = _teamService.GetTeamAndPlayer();
            return result.Data;
        }
    }
}
