using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Maç ve takım davetleri (docs/social-api.md "Davetler").</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvitesController : ControllerBase
    {
        private readonly IInviteService _inviteService;

        public InvitesController(IInviteService inviteService)
        {
            _inviteService = inviteService;
        }

        [HttpPost("InviteToMatch")]
        public IActionResult InviteToMatch(InviteToMatchModel model)
        {
            var result = _inviteService.InviteToMatch(User.GetPlayerId(), User.IsAdmin(), model.MatchId, model.PlayerIds);
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, result.Data.Invited, result.Data.Skipped }
                : null);
        }

        /// <summary>"Yedek bul" adayları (maçı yöneten). docs/reliability-api.md</summary>
        [HttpGet("SubstituteCandidates")]
        public IActionResult SubstituteCandidates(int matchId)
        {
            var result = _inviteService.GetSubstituteCandidates(User.GetPlayerId(), User.IsAdmin(), matchId);
            return this.ToHttp(result, result.Data);
        }

        /// <summary>Seçilen oyunculara "yer açıldı" bildirimi (maç daveti olarak; ilk kabul eden yeri alır).</summary>
        [HttpPost("CallSubstitutes")]
        public IActionResult CallSubstitutes(InviteToMatchModel model)
        {
            var result = _inviteService.CallSubstitutes(User.GetPlayerId(), User.IsAdmin(), model.MatchId, model.PlayerIds);
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, result.Data.Invited, result.Data.Skipped }
                : null);
        }

        [HttpPost("InviteToTeam")]
        public IActionResult InviteToTeam(InviteToTeamModel model)
        {
            var result = _inviteService.InviteToTeam(User.GetPlayerId(), model.TeamId, model.PlayerIds);
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, result.Data.Invited, result.Data.Skipped }
                : null);
        }

        // Sadece bekleyen davetler, yeniden eskiye
        [HttpGet("My")]
        public ActionResult<MyInvitesDto> My()
        {
            return _inviteService.GetMyInvites(User.GetPlayerId());
        }

        [HttpGet("PendingCount")]
        public IActionResult PendingCount()
        {
            return Ok(new { Count = _inviteService.GetPendingCount(User.GetPlayerId()) });
        }

        [HttpPost("Respond")]
        public IActionResult Respond(RespondInviteModel model)
        {
            var result = _inviteService.Respond(User.GetPlayerId(), model.Kind, model.InviteId, model.Accept);
            return this.ToHttp(result, new { Success = true, result.Message, Status = result.Data });
        }
    }
}
