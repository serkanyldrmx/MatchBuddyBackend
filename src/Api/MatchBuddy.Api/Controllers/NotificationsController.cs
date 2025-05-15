using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // GET api/notifications/GetAllNotifications
        // Tüm bildirimleri almak için kullanılır.
        [HttpGet("GetAllNotifications")]
        public IActionResult GetAllNotifications()
        {
            var result = _notificationService.GetAllNotifications();
            if (result.Success)
                return Ok(result.Data); // Başarılıysa bildirimleri döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }

        // GET api/notifications/GetNotificationById?notificationId={id}
        // ID'si belirtilen bildirim bilgilerini almak için kullanılır.
        [HttpGet("GetNotificationById")]
        public IActionResult GetNotificationById(int notificationId)
        {
            var result = _notificationService.GetById(notificationId);
            if (result.Success)
                return Ok(result.Data); // Başarılıysa belirtilen bildirim verisini döndür
            return NotFound(result.Message); // Bildirim bulunamazsa 404 döndür
        }

        // GET api/notifications/GetNotificationsByPlayerId?playerId={id}
        // Belirli bir oyuncuya ait bildirimleri almak için kullanılır.
        [HttpGet("GetNotificationsByPlayerId")]
        public IActionResult GetNotificationsByPlayerId(int playerId)
        {
            var result = _notificationService.GetUnreadNotificationsByPlayerId(playerId);
            if (result.Success)
                return Ok(result.Data); // Başarılıysa belirtilen oyuncuya ait bildirimleri döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }

        // POST api/notifications/AddNotification
        // Yeni bir bildirim eklemek için kullanılır.
        [HttpPost("AddNotification")]
        public IActionResult AddNotification(Notification notification)
        {
            var result = _notificationService.Add(notification);
            if (result.Success)
                return Ok(result.Message); // Başarılıysa mesajı döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }

        // PUT api/notifications/UpdateNotification
        // Mevcut bir bildirimi güncellemek için kullanılır.
        [HttpPut("UpdateNotification")]
        public IActionResult UpdateNotification(Notification notification)
        {
            var result = _notificationService.Update(notification);
            if (result.Success)
                return Ok(result.Message); // Başarılıysa mesajı döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }

        // DELETE api/notifications/DeleteNotification?notificationId={id}
        // Belirli bir bildirimi silmek için kullanılır.
        [HttpDelete("DeleteNotification")]
        public IActionResult DeleteNotification(int notificationId)
        {
            var result = _notificationService.Delete(notificationId);
            if (result.Success)
                return Ok(result.Message); // Başarılıysa mesajı döndür
            return NotFound(result.Message); // Bildirim bulunamazsa 404 döndür
        }

        // POST api/notifications/SendToPlayers
        // Birden fazla oyuncuya bildirim göndermek için kullanılır. 
        // Bildirim metni, türü, maç ve stadyum ID'leri ve oyuncu ID'leri ile bildirim gönderilir.
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
                PlayerNotifications = new List<PlayerNotification>() // Bu kısım, veritabanında ilişki kurmak için kullanılabilir
            };

            var result = _notificationService.SendNotificationToPlayers(model.PlayerIds, notification);
            if (result.Success)
                return Ok(result.Message); // Başarılıysa mesajı döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }

        // POST api/notifications/MarkAsRead?playerNotificationId={id}
        // Belirli bir bildirimi okundu olarak işaretlemek için kullanılır.
        [HttpPost("MarkAsRead")]
        public IActionResult MarkAsRead(int playerNotificationId)
        {
            var result = _notificationService.MarkAsRead(playerNotificationId);
            if (result.Success)
                return Ok(result.Message); // Başarılıysa mesajı döndür
            return BadRequest(result.Message); // Başarısızsa hata mesajı döndür
        }
    }
}
