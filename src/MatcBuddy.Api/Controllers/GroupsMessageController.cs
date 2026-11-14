using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Api.Services;
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
    public class GroupsMessageController : ControllerBase
    {
        private readonly IGroupMessageService _groupMessageService;
        private readonly IGroupPlayerService _groupPlayerService;
        private readonly IMediaService _mediaService;
        private readonly IChatService _chatService;

        public GroupsMessageController(IGroupMessageService groupMessageService, IGroupPlayerService groupPlayerService, IMediaService mediaService,
            IChatService chatService)
        {
            _chatService = chatService;
            _mediaService = mediaService;
            _groupMessageService = groupMessageService;
            _groupPlayerService = groupPlayerService;
        }

        private bool CanAccessGroup(int groupId) =>
            User.IsAdmin() || _groupPlayerService.IsPlayerInGroup(groupId, User.GetPlayerId());

        [HttpPost("SaveGroupMessage")]
        public IActionResult SaveGroupMessage(GroupMessageModel groupMessageModel, [FromServices] MatcBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            // Metin ya da fotoğraf (mediaId) dolu olmalı
            if (string.IsNullOrWhiteSpace(groupMessageModel.MatchMessage) && groupMessageModel.MediaId == null)
            {
                return this.BadRequestError(ErrorCodes.Message.Empty);
            }
            if (groupMessageModel.MatchMessage != null && groupMessageModel.MatchMessage.Trim().Length > MessageController.MaxMessageLength)
            {
                return this.BadRequestError(ErrorCodes.Message.TooLong, MessageController.MaxMessageLength);
            }

            if (!CanAccessGroup(groupMessageModel.GroupId))
            {
                return Forbid();
            }

            // Yanıt: aynı gruptan bir mesaj olmalı
            if (groupMessageModel.ReplyToGroupMessageId.HasValue
                && !_chatService.IsValidReply(true, groupMessageModel.ReplyToGroupMessageId.Value, User.GetPlayerId(), groupMessageModel.GroupId))
            {
                return this.BadRequestError(ErrorCodes.Message.ReplyInvalid);
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
                MatchMessage = groupMessageModel.MatchMessage?.Trim() ?? string.Empty,
                SendingDate = DateTime.Now,
                SendPlayerId = User.GetPlayerId(), // Gönderen her zaman token sahibidir
                Status = groupMessageModel.Status,
                MediaId = groupMessageModel.MediaId,
                ImageUrl = imageUrl,
                ReplyToGroupMessageId = groupMessageModel.ReplyToGroupMessageId,
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
        public ActionResult<List<ChatMessageDto>> GetGroupMessageById(GroupMessageModel groupMessageModel)
        {
            if (!CanAccessGroup(groupMessageModel.GroupId))
            {
                return Forbid();
            }
            // Gönderen fotoğrafı, yanıt önizlemesi, tepkiler ve silinme bilgisiyle (docs/engagement-api.md §1)
            return _chatService.GroupConversation(User.GetPlayerId(), groupMessageModel.GroupId);
        }
    }
}
