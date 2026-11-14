using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Güvenilirlik puanı: ceza önizlemesi, gelmeyenleri işaretleme, mazeret kabulü, oyuncu özeti — docs/reliability-api.md.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReliabilityController : ControllerBase
    {
        private readonly IReliabilityDal _dal;

        public ReliabilityController(IReliabilityDal dal) => _dal = dal;

        /// <summary>"Maça gelemiyorum" penceresi: kalan saat ve düşecek puan.</summary>
        [HttpGet("LeavePreview")]
        public IActionResult LeavePreview(int matchId)
        {
            var result = _dal.LeavePreview(matchId, User.GetPlayerId());
            if (!result.Success) return this.ToHttp(result);
            return Ok(result.Data);
        }

        /// <summary>Kurucu: maçtan sonra gelmeyenler (tam liste; tekrar çağrı günceller).</summary>
        [HttpPost("MarkNoShows")]
        public IActionResult MarkNoShows(MarkNoShowsModel model) =>
            this.ToHttp(_dal.MarkNoShows(model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.PlayerIds ?? new()));

        /// <summary>Kurucu: oyuncunun bu maçtaki cezasını mazeretli sayar (puan iade).</summary>
        [HttpPost("AcceptExcuse")]
        public IActionResult AcceptExcuse(AcceptExcuseModel model) =>
            this.ToHttp(_dal.AcceptExcuse(model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.PlayerId));

        /// <summary>Oyuncu özeti; geçmiş yalnızca kendisi ve admin için.</summary>
        [HttpGet("Player")]
        public ActionResult<ReliabilitySummaryDto> Player(int playerId) =>
            _dal.Summary(playerId, User.IsSelfOrAdmin(playerId));

        /// <summary>Kurucu: maçtan son dakika çekilenler ve gelmeyenler (mazeretleriyle).</summary>
        [HttpGet("Withdrawals")]
        public IActionResult Withdrawals(int matchId, [FromServices] MatcBuddy.Business.Abstract.IMatchParticipationService participation)
        {
            var permission = participation.CheckCanManage(matchId, User.GetPlayerId(), User.IsAdmin());
            if (!permission.Success) return this.ToHttp(permission);
            return Ok(_dal.Withdrawals(matchId));
        }
    }
}
