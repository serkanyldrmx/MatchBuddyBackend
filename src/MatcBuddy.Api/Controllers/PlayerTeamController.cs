using MatcBuddy.Api.Model;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.Entity;
using MatcBuddy.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlayerTeamController : ControllerBase
    {
        private readonly IPlayerTeamService _playerTeamService;
        private readonly ITeamService _teamService;
        private readonly ISocialService _socialService;
        public PlayerTeamController(IPlayerTeamService playerTeamService, ITeamService teamService, ISocialService socialService)
        {
            _playerTeamService = playerTeamService;
            _teamService = teamService;
            _socialService = socialService;
        }

        [HttpPost("SavePLayerTeam")]
        public IActionResult SavePLayerTeam(PlayerTeamModel playerTeamModel)
        {
            // Kişi kendini bir takıma ekleyebilir (maça katılma).
            // Başka birini eklemek için takımın oyuncusu veya admin olmak gerekir.
            var me = User.GetPlayerId();
            var addingSelf = playerTeamModel.PlayerId == me;
            if (!addingSelf && !User.IsAdmin() && !_teamService.IsPlayerInTeam(playerTeamModel.TeamId, me))
            {
                return Forbid();
            }
            // İşletme hesapları takıma eklenemez
            if (_socialService.OwnerIds(new[] { playerTeamModel.PlayerId }).Count > 0)
            {
                return this.BadRequestError(ErrorCodes.Team.OwnerNotAllowed);
            }
            if (_teamService.IsPlayerInTeam(playerTeamModel.TeamId, playerTeamModel.PlayerId))
            {
                return this.BadRequestError(ErrorCodes.Team.PlayerAlreadyInTeam);
            }

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
            return this.ToHttp(result);
        }
    }
}
