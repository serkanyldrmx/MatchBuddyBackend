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
    public class MessageController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly ISocialService _socialService;
        private readonly IMediaService _mediaService;

        public MessageController(IMessageService messageService, ISocialService socialService, IMediaService mediaService)
        {
            _mediaService = mediaService;
            _messageService = messageService;
            _socialService = socialService;
        }

        // Kullanıcı sadece kendi gönderdiği/aldığı mesajları görebilir (admin hariç)
        [HttpGet("GetSendMessages")]
        public ActionResult<List<Message>> GetSendMessages([FromQuery] int playerId)
        {
            if (!User.IsSelfOrAdmin(playerId)) return Forbid();
            return _messageService.GetSendMessage(playerId).Data;
        }

        [HttpGet("GetRecipientMessages")]
        public ActionResult<List<Message>> GetRecipientMessage([FromQuery] int playerId)
        {
            if (!User.IsSelfOrAdmin(playerId)) return Forbid();
            return _messageService.GetRecipientMessage(playerId).Data;
        }

        // İki kişi arasındaki konuşma: istek sahibi taraflardan biri olmalı
        [HttpPost("GetMessageById")]
        public ActionResult<List<Message>> GetMessageById(MessageModel messageModel)
        {
            var me = User.GetPlayerId();
            if (!User.IsAdmin() && messageModel.SendPlayerId != me && messageModel.RecipientPlayerId != me)
            {
                return Forbid();
            }

            var model = new GetMessageModel()
            {
                SendPlayerId = messageModel.SendPlayerId,
                RecipientPlayerId = messageModel.RecipientPlayerId
            };
            return _messageService.GetMessageById(model).Data;
        }

        [HttpPost("MessageSave")]
        public IActionResult MessageSave(MessageSaveModel mesageModel)
        {
            // Metin ya da fotoğraf (mediaId) dolu olmalı
            if (string.IsNullOrWhiteSpace(mesageModel.MatchMessage) && mesageModel.MediaId == null)
            {
                return BadRequest(new { Success = false, Message = "Mesaj boş olamaz." });
            }

            // Mesaj izni (engel, alıcının MessagePermission ayarı, önceki yazışma): izin yoksa 403
            var permission = _socialService.CheckCanMessage(User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0);
            if (!permission.Success) return this.ToHttp(permission);

            // Fotoğraf: gönderene ait, Pending, MessageImage olmalı -> Attached
            string? imageUrl = null;
            if (mesageModel.MediaId.HasValue)
            {
                var attach = _mediaService.Attach(mesageModel.MediaId.Value, MediaKind.MessageImage, User.GetPlayerId());
                if (!attach.Success) return this.ToHttp(attach);
                imageUrl = attach.Data.Url;
            }

            var message = new Message()
            {
                SendPlayerId = User.GetPlayerId(), // Gönderen her zaman token sahibidir
                RecipientPlayerId = mesageModel.RecipientPlayerId,
                MatchMessage = mesageModel.MatchMessage ?? string.Empty,
                SendingDate = mesageModel.SendingDate,
                Status = mesageModel.Status,
                MediaId = mesageModel.MediaId,
                ImageUrl = imageUrl,
            };
            var result = _messageService.Add(message);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
