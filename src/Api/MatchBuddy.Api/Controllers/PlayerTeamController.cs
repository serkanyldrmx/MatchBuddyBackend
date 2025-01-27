using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayerTeamController : ControllerBase
    {
        private readonly IPlayerTeamService _playerTeamService;
        public PlayerTeamController(IPlayerTeamService playerTeamService)
        {
            _playerTeamService = playerTeamService;
        }

        [HttpPost("SavePLayerTeam")]
        public IActionResult SavePLayerTeam(PlayerTeamModel playerTeamModel)
        {
            var playerTeam = new PlayerTeam()
            {
                PlayerId = playerTeamModel.PlayerId,
                TeamId = playerTeamModel.TeamId,
            };
            var result = _playerTeamService.Add(playerTeam);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
