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

        [HttpPost("SaveTeamByPlayerId")]
        public IActionResult SaveTeamByPlayerId(TeamPlayerIdModel playerIdModel)
        {
            if (playerIdModel.PlayerIds != null && playerIdModel.TeamId != null)
            {
                var result1 = _teamService.AddPlayerById(playerIdModel.PlayerIds, playerIdModel.TeamId);

                if (!result1.Success)
                {
                    return BadRequest(result1);
                }

                return Ok(true); // tüm kullanıcılar başarıyla eklendiyse burada dönüş yapılır
            }
            else
            {
                return BadRequest("PlayerId veya TeamId null olamaz.");
            }
        }


        //Maç silmek için
        [HttpPost("DeleteTeam")]
        public IActionResult DeleteTeam(int teamId)
        {
            var team = new Team()
            {
                TeamId = teamId,
            };
            var result = _teamService.Delete(team);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpGet("GetTeamList")]
        public List<GetTeamAndPlayer> GetTeamList()
        {
            ITeamService teamService = new TeamManager(new EFteamDal());
            var result = _teamService.GetTeamAndPlayer();
            return result.Data;
        }
    }
}
