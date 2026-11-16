using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Api.Services.Push;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
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

        // Oyuncunun kendi bildirimleri (admin başkasınınkini de görebilir)
        [HttpGet("GetNotificationsByPlayerId")]
        public IActionResult GetNotificationsByPlayerId(int playerId)
        {
            if (!User.IsSelfOrAdmin(playerId)) return Forbid();

            var result = _notificationService.GetUnreadNotificationsByPlayerId(playerId);
            if (result.Success)
            {
                foreach (var n in result.Data)
                {
                    if (n.Notification != null)
                        n.Url = PushDispatchService.UrlFor(n.Notification.Type, n.Notification.MatchId, n.Notification.StadiumId);
                }
                return Ok(result.Data);
            }
            return this.ToHttp(result);
        }

        // Kendi bildirimlerinin hepsini okundu işaretle
        [HttpPost("MarkAllAsRead")]
        public IActionResult MarkAllAsRead()
        {
            var result = _notificationService.MarkAllAsRead(User.GetPlayerId());
            return Ok(result.Message);
        }

        // Okundu işaretle: sadece bildirimin sahibi
        [HttpPost("MarkAsRead")]
        public IActionResult MarkAsRead(int playerNotificationId)
        {
            var result = _notificationService.MarkAsRead(playerNotificationId, User.GetPlayerId());
            if (result.Success)
                return Ok(result.Message);
            return this.ToHttp(result);
        }
    }
}
