using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MatchCommentController : ControllerBase
    {
        private readonly IMatchCommentsService _matchCommentsService;

        public MatchCommentController(IMatchCommentsService matchCommentsService)
        {
            _matchCommentsService = matchCommentsService;
        }

        // Yorum ekle: yorumu yazan her zaman token sahibidir
        [HttpPost("SaveMatchComment")]
        public IActionResult SaveMatchComment(MatchCommentModel matchCommentModel, [FromServices] MatcBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            if (string.IsNullOrWhiteSpace(matchCommentModel.Comment))
            {
                return this.BadRequestError(ErrorCodes.Comment.Empty);
            }

            var matchComment = new MatchComment()
            {
                Comment = matchCommentModel.Comment,
                MatchId = matchCommentModel.MatchId,
                playerId = User.GetPlayerId(),
            };
            var result = _matchCommentsService.Add(matchComment);
            if (result.Success)
            {
                _ = realtime.MatchCommentAsync(matchComment.MatchId); // anlık: maçı açık tutanlar yenilesin
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        // Yorum sil: sadece yorumun sahibi veya admin
        [HttpPost("DeleteMatchComment")]
        public IActionResult DeleteMatchComment(int commentId, [FromServices] MatcBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var comment = _matchCommentsService.GetComment(commentId);
            if (comment == null)
            {
                return this.NotFoundError(ErrorCodes.Comment.NotFound);
            }
            if (!User.IsSelfOrAdmin(comment.playerId))
            {
                return Forbid();
            }

            var result = _matchCommentsService.Delete(comment);
            if (result.Success)
            {
                _ = realtime.MatchCommentAsync(comment.MatchId);
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpGet("GetMatchCommentList")]
        public List<MatchComment> GetMatchCommentList()
        {
            return _matchCommentsService.GetAll().Data;
        }

        [HttpGet("GetMatchComments")]
        public List<MatchComentsDto> GetMatchComments([FromQuery] int matchId)
        {
            return _matchCommentsService.GetById(matchId).Data;
        }
    }
}
