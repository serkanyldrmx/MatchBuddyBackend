using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Tüm bildirimler: sadece admin
        [Authorize(Roles = Roles.Admin)]
        [HttpGet("GetAllNotifications")]
        public IActionResult GetAllNotifications()
        {
            var result = _notificationService.GetAllNotifications();
            if (result.Success)
                return Ok(result.Data);
            return BadRequest(result.Message);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpGet("GetNotificationById")]
        public IActionResult GetNotificationById(int notificationId)
        {
            var result = _notificationService.GetById(notificationId);
            if (result.Success && result.Data != null)
                return Ok(result.Data);
            return NotFound(result.Message);
        }

        // Oyuncunun kendi bildirimleri (admin başkasınınkini de görebilir)
        [HttpGet("GetNotificationsByPlayerId")]
        public IActionResult GetNotificationsByPlayerId(int playerId)
        {
            if (!User.IsSelfOrAdmin(playerId)) return Forbid();

            var result = _notificationService.GetUnreadNotificationsByPlayerId(playerId);
            if (result.Success)
                return Ok(result.Data);
            return BadRequest(result.Message);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost("AddNotification")]
        public IActionResult AddNotification(Notification notification)
        {
            var result = _notificationService.Add(notification);
            if (result.Success)
                return Ok(result.Message);
            return BadRequest(result.Message);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPut("UpdateNotification")]
        public IActionResult UpdateNotification(Notification notification)
        {
            var result = _notificationService.Update(notification);
            if (result.Success)
                return Ok(result.Message);
            return BadRequest(result.Message);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("DeleteNotification")]
        public IActionResult DeleteNotification(int notificationId)
        {
            var result = _notificationService.Delete(notificationId);
            if (result.Success)
                return Ok(result.Message);
            return NotFound(result.Message);
        }

        // Birden fazla oyuncuya bildirim gönderme: sadece admin
        [Authorize(Roles = Roles.Admin)]
        [HttpPost("SendToPlayers")]
        public IActionResult SendNotificationToPlayers([FromBody] NotificationSendModel model)
        {
            var notification = new Notification
            {
                Text = model.Text,
                Type = model.Type,
                CreatedAt = DateTime.Now,
                IsRead = false,
                MatchId = model.MatchId,
                StadiumId = model.StadiumId,
                PlayerNotifications = new List<PlayerNotification>()
            };

            var result = _notificationService.SendNotificationToPlayers(model.PlayerIds, notification);
            if (result.Success)
                return Ok(result.Message);
            return BadRequest(result.Message);
        }

        // Okundu işaretle: sadece bildirimin sahibi
        [HttpPost("MarkAsRead")]
        public IActionResult MarkAsRead(int playerNotificationId)
        {
            var result = _notificationService.MarkAsRead(playerNotificationId, User.GetPlayerId());
            if (result.Success)
                return Ok(result.Message);
            return BadRequest(result.Message);
        }
    }
}
