using MatcBuddy.Api.Model;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Api.Security;
using Microsoft.AspNetCore.Authorization;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TeamController : ControllerBase
    {
        ITeamService _teamService;
        IPlayerTeamService _playerTeamService;
        ISocialService _socialService;

        public TeamController(ITeamService teamService, IPlayerTeamService playerTeamService, ISocialService socialService)
        {
            _teamService = teamService;
            _playerTeamService = playerTeamService;
            _socialService = socialService;
        }

        private const string OwnerNotAllowed = ErrorCodes.Team.OwnerNotAllowed;

        [HttpPost("SaveTeam")]
        public IActionResult SaveTeam(TeamModel teamModel)
        {
            // İşletme hesabı takım kuramaz, takıma eklenemez (docs/venues-api.md)
            if (User.IsOwner())
                return this.ForbiddenError(ErrorCodes.Team.OwnerCannotCreate);
            if (teamModel.PlayerId != null && _socialService.OwnerIds(teamModel.PlayerId).Count > 0)
                return this.BadRequestError(OwnerNotAllowed);

            var team = new Team
            {
                TeamName = teamModel.TeamName,
            };
            var result = _teamService.Add(team, User.GetPlayerId());
            if (result.Success)
            {
                foreach (var playerId in teamModel.PlayerId.Distinct()) // aynı oyuncu iki kez eklenmesin
                {
                    var playerTeam = new PlayerTeam()
                    {
                        PlayerId = playerId,
                        TeamId = team.TeamId // group nesnesinin Id'sini kullanarak gruba ait olduğu belirtiliyor
                    };

                    var result1 = _playerTeamService.Add(playerTeam);

                    if (!result1.Success)
                    {
                        return this.ToHttp(result1); // Eğer ekleme işlemi başarısız olursa, döngüyü sonlandırıp hata döndürüyoruz
                    }
                }

                return Ok(result);
            }
            return this.ToHttp(result);
        }

        [HttpPost("SaveTeamByPlayerId")]
        public IActionResult SaveTeamByPlayerId(TeamPlayerIdModel playerIdModel)
        {
            if (playerIdModel.PlayerIds != null && playerIdModel.TeamId > 0)
            {
                // Takım kadrosunu düzenleme: kurucu (kurucusu bilinmeyen eski takımlarda her üye) veya admin
                if (!User.IsAdmin() && !_teamService.CanManage(playerIdModel.TeamId, User.GetPlayerId()))
                {
                    return this.ForbiddenError(ErrorCodes.Team.CreatorOnly);
                }
                if (_socialService.OwnerIds(playerIdModel.PlayerIds).Count > 0)
                {
                    return this.BadRequestError(OwnerNotAllowed);
                }

                var result1 = _teamService.AddPlayerById(playerIdModel.PlayerIds, playerIdModel.TeamId);

                if (!result1.Success)
                {
                    return this.ToHttp(result1);
                }

                return Ok(true); // tüm kullanıcılar başarıyla eklendiyse burada dönüş yapılır
            }
            else
            {
                return this.BadRequestError(ErrorCodes.Team.PlayerIdsOrTeamIdRequired);
            }
        }


        //Maç silmek için
        [HttpPost("DeleteTeam")]
        public IActionResult DeleteTeam(int teamId)
        {
            // Takım silme: kurucu (kurucusu bilinmeyen eski takımlarda her üye) veya admin
            if (!User.IsAdmin() && !_teamService.CanManage(teamId, User.GetPlayerId()))
            {
                return this.ForbiddenError(ErrorCodes.Team.CreatorOnly);
            }

            var team = new Team()
            {
                TeamId = teamId,
            };
            var result = _teamService.Delete(team);
            if (result.Success)
            {
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        // Takımdan ayrıl: kurucu ayrılırsa kuruculuk takımdaki en eski oyuncuya geçer
        [HttpPost("LeaveTeam")]
        public IActionResult LeaveTeam(int teamId)
        {
            var result = _teamService.Leave(teamId, User.GetPlayerId());
            return result.Success ? Ok(result) : this.ToHttp(result);
        }

        [HttpGet("GetTeamList")]
        public List<GetTeamAndPlayer> GetTeamList()
        {
            var result = _teamService.GetTeamAndPlayer();
            return result.Data;
        }
    }
}
