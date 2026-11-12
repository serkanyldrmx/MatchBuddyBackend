using MatcBuddy.Api.Model;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface INotificationService
    {
        IDataResult<List<Notification>> GetAllNotifications();
        // Tüm bildirimleri getir

        IDataResult<List<PlayerNotificationDto>> GetUnreadNotificationsByPlayerId(int playerId);
        // Belirli bir oyuncuya ait bildirimleri getir

        IDataResult<Notification> GetById(int notificationId);
        // Bildirimi ID'ye göre getir

        IResult Add(Notification notification);
        // Yeni bildirim ekle

        IResult Update(Notification notification);
        // Bildirimi güncelle (örneğin metni veya tipi)

        IResult Delete(int notificationId);
        // Bildirimi sil

        IResult MarkAsRead(int playerNotificationId, int playerId);
        // Oyuncuya ait bildirimi okundu olarak işaretle

        IResult SendNotificationToPlayers(List<int> playerIds, Notification notification);
        // Belirli kullanıcılara bildirim gönder
    }

}
