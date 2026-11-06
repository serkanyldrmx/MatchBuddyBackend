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
    public class GroupsMessageController : ControllerBase
    {
        private readonly IGroupMessageService _groupMessageService;
        private readonly IGroupPlayerService _groupPlayerService;

        public GroupsMessageController(IGroupMessageService groupMessageService, IGroupPlayerService groupPlayerService)
        {
            _groupMessageService = groupMessageService;
            _groupPlayerService = groupPlayerService;
        }

        private bool CanAccessGroup(int groupId) =>
            User.IsAdmin() || _groupPlayerService.IsPlayerInGroup(groupId, User.GetPlayerId());

        [HttpPost("SaveGroupMessage")]
        public IActionResult SaveGroupMessage(GroupMessageModel groupMessageModel)
        {
            if (!CanAccessGroup(groupMessageModel.GroupId))
            {
                return Forbid();
            }

            var groupMessage = new GroupMessage()
            {
                GroupId = groupMessageModel.GroupId,
                MatchMessage = groupMessageModel.MatchMessage,
                SendingDate = groupMessageModel.SendingDate,
                SendPlayerId = User.GetPlayerId(), // Gönderen her zaman token sahibidir
                Status = groupMessageModel.Status
            };

            var result = _groupMessageService.Add(groupMessage);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpPost("GetGroupMessageById")]
        public ActionResult<List<GroupMessageModelDto>> GetGroupMessageById(GroupMessageModel groupMessageModel)
        {
            if (!CanAccessGroup(groupMessageModel.GroupId))
            {
                return Forbid();
            }
            return _groupMessageService.GetGroupMessageById(groupMessageModel.GroupId).Data;
        }
    }
}
