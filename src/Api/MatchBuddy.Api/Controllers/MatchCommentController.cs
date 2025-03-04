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
    public class MatchCommentController : ControllerBase
    {
        IMatchCommentsService _matchCommentsService;
        
        public MatchCommentController(IMatchCommentsService matchCommentsService)
        {
            _matchCommentsService = matchCommentsService;
        }

        //Maç eklemek için 
        [HttpPost("SaveMatchComment")]
        public IActionResult SaveMatchComment(MatchCommentModel matchCommentModel)
        {
            var matchComment = new MatchComment() 
            {
               Comment= matchCommentModel.Comment,
               MatchId=matchCommentModel.MatchId,
               playerId =matchCommentModel.playerId,               
            };
            var result = _matchCommentsService.Add(matchComment);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }


        //Maç silmek için
        [HttpPost("DeleteMatchComment")]
        public IActionResult DeleteMatchComment(int commentId)
        {
            var matchComment = new MatchComment()
            {
                CommentsId = commentId,
            };
            var result = _matchCommentsService.Delete(matchComment);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpGet("GetMatchCommentList")]
        public List<MatchComment> GetMatchCommentList()
        {
            var result = _matchCommentsService.GetAll();
            return result.Data;
        }

        [HttpGet("GetMatchComments")]
        public List<MatchComentsDto> GetMatchComments([FromQuery] int matchId)
        {
            var result = _matchCommentsService.GetById(matchId);
            return result.Data;
        }
    }
}
