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
        private readonly IChatService _chatService;

        /// <summary>Mesaj metni üst sınırı (Messages.MatchMessage nvarchar(1000)).</summary>
        public const int MaxMessageLength = 1000;

        public MessageController(IMessageService messageService, ISocialService socialService, IMediaService mediaService, IChatService chatService)
        {
            _chatService = chatService;
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
        public ActionResult<List<ChatMessageDto>> GetMessageById(MessageModel messageModel)
        {
            var me = User.GetPlayerId();
            if (!User.IsAdmin() && messageModel.SendPlayerId != me && messageModel.RecipientPlayerId != me)
            {
                return Forbid();
            }

            // Yanıt önizlemesi, tepkiler ve silinme bilgisiyle (docs/engagement-api.md §1)
            var a = messageModel.SendPlayerId ?? 0;
            var b = messageModel.RecipientPlayerId ?? 0;
            var viewer = a == me || b == me ? me : a;
            var other = viewer == a ? b : a;
            return _chatService.DirectConversation(viewer, other);
        }

        [HttpPost("MessageSave")]
        public IActionResult MessageSave(MessageSaveModel mesageModel, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            // Metin ya da fotoğraf (mediaId) dolu olmalı
            if (string.IsNullOrWhiteSpace(mesageModel.MatchMessage) && mesageModel.MediaId == null)
            {
                return this.BadRequestError(ErrorCodes.Message.Empty);
            }
            if (mesageModel.MatchMessage != null && mesageModel.MatchMessage.Trim().Length > MaxMessageLength)
            {
                return this.BadRequestError(ErrorCodes.Message.TooLong, MaxMessageLength);
            }

            // Mesaj izni (engel, alıcının MessagePermission ayarı, önceki yazışma): izin yoksa 403
            var permission = _socialService.CheckCanMessage(User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0);
            if (!permission.Success) return this.ToHttp(permission);

            // Yanıt: aynı konuşmadan bir mesaj olmalı
            if (mesageModel.ReplyToMessageId.HasValue
                && !_chatService.IsValidReply(false, mesageModel.ReplyToMessageId.Value, User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0))
            {
                return this.BadRequestError(ErrorCodes.Message.ReplyInvalid);
            }

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
                MatchMessage = mesageModel.MatchMessage?.Trim() ?? string.Empty,
                // Sunucu saati: istemci saati yanlış olsa da sıralama ve okunmamış sayısı doğru kalsın
                SendingDate = DateTime.Now,
                Status = mesageModel.Status,
                MediaId = mesageModel.MediaId,
                ImageUrl = imageUrl,
                ReplyToMessageId = mesageModel.ReplyToMessageId,
            };
            var result = _messageService.Add(message);
            if (result.Success)
            {
                // Yanıt yazan, karşı tarafın mesajlarını görmüş demektir
                if (_chatService.MarkDirectRead(User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0) > 0)
                    _ = realtime.DirectReadAsync(User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0);
                _ = realtime.DirectMessageAsync(User.GetPlayerId(), mesageModel.RecipientPlayerId ?? 0);
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        /// <summary>Bir oyuncuyla konuşmayı okundu yap (konuşma açıldığında / yeni mesaj geldiğinde). Gönderen "görüldü"yü anında görür.</summary>
        [HttpPost("MarkRead")]
        public IActionResult MarkRead(MarkReadModel model, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var count = _chatService.MarkDirectRead(User.GetPlayerId(), model.PlayerId);
            if (count > 0) _ = realtime.DirectReadAsync(User.GetPlayerId(), model.PlayerId);
            return Ok(new { success = true, count });
        }

        /// <summary>Mesaja tepki ver / değiştir / kaldır (emoji null ya da aynı emoji = kaldır). Birebir ve grup mesajı için ortak.</summary>
        [HttpPost("React")]
        public IActionResult React(ReactInput input, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var result = _chatService.React(User.GetPlayerId(), input);
            if (result.Success) _ = realtime.ConversationChangedAsync(result.Data);
            return result.Success ? Ok(new { success = true }) : this.ToHttp(result);
        }

        /// <summary>Kendi mesajını sil (birebir ya da grup). Konuşmada "Bu mesaj silindi" olarak kalır.</summary>
        [HttpPost("Delete")]
        public IActionResult Delete(DeleteMessageModel model, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var result = _chatService.Delete(User.GetPlayerId(), model.MessageId, model.GroupMessageId);
            if (result.Success) _ = realtime.ConversationChangedAsync(result.Data);
            return result.Success ? Ok(new { success = true, message = result.Message }) : this.ToHttp(result);
        }

        /// <summary>İzin verilen tepki emojileri.</summary>
        [HttpGet("Reactions")]
        public ActionResult<IReadOnlyList<string>> Reactions() =>
            Ok(MatchBuddy.DataAccess.Concrete.EntityFramework.EFChatDal.AllowedReactions);

        /// <summary>Mesaj simgesindeki rozet: okunmamış birebir ve grup mesajı sayısı.</summary>
        [HttpGet("Unread")]
        public ActionResult<UnreadSummaryDto> Unread() => _chatService.Unread(User.GetPlayerId());
    }
}
