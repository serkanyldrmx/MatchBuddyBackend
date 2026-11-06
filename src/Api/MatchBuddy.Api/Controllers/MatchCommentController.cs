using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
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
        public IActionResult SaveMatchComment(MatchCommentModel matchCommentModel)
        {
            if (string.IsNullOrWhiteSpace(matchCommentModel.Comment))
            {
                return BadRequest(new { Success = false, Message = "Yorum boş olamaz." });
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
                return Ok(result);
            }
            return BadRequest(result);
        }

        // Yorum sil: sadece yorumun sahibi veya admin
        [HttpPost("DeleteMatchComment")]
        public IActionResult DeleteMatchComment(int commentId)
        {
            var comment = _matchCommentsService.GetComment(commentId);
            if (comment == null)
            {
                return NotFound(new { Success = false, Message = "Yorum bulunamadı." });
            }
            if (!User.IsSelfOrAdmin(comment.playerId))
            {
                return Forbid();
            }

            var result = _matchCommentsService.Delete(comment);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
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
