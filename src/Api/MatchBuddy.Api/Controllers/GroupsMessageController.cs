using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
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
        private readonly IMediaService _mediaService;

        public GroupsMessageController(IGroupMessageService groupMessageService, IGroupPlayerService groupPlayerService, IMediaService mediaService)
        {
            _mediaService = mediaService;
            _groupMessageService = groupMessageService;
            _groupPlayerService = groupPlayerService;
        }

        private bool CanAccessGroup(int groupId) =>
            User.IsAdmin() || _groupPlayerService.IsPlayerInGroup(groupId, User.GetPlayerId());

        [HttpPost("SaveGroupMessage")]
        public IActionResult SaveGroupMessage(GroupMessageModel groupMessageModel, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            // Metin ya da fotoğraf (mediaId) dolu olmalı
            if (string.IsNullOrWhiteSpace(groupMessageModel.MatchMessage) && groupMessageModel.MediaId == null)
            {
                return this.BadRequestError(ErrorCodes.Message.Empty);
            }

            if (!CanAccessGroup(groupMessageModel.GroupId))
            {
                return Forbid();
            }

            // Fotoğraf: gönderene ait, Pending, MessageImage olmalı -> Attached
            string? imageUrl = null;
            if (groupMessageModel.MediaId.HasValue)
            {
                var attach = _mediaService.Attach(groupMessageModel.MediaId.Value, MediaKind.MessageImage, User.GetPlayerId());
                if (!attach.Success) return this.ToHttp(attach);
                imageUrl = attach.Data.Url;
            }

            var groupMessage = new GroupMessage()
            {
                GroupId = groupMessageModel.GroupId,
                MatchMessage = groupMessageModel.MatchMessage ?? string.Empty,
                SendingDate = groupMessageModel.SendingDate,
                SendPlayerId = User.GetPlayerId(), // Gönderen her zaman token sahibidir
                Status = groupMessageModel.Status,
                MediaId = groupMessageModel.MediaId,
                ImageUrl = imageUrl,
            };

            var result = _groupMessageService.Add(groupMessage);
            if (result.Success)
            {
                _ = realtime.GroupMessageAsync(groupMessageModel.GroupId, User.GetPlayerId());
                return Ok(result);
            }
            return this.ToHttp(result);
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
