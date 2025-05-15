using MatchBuddy.Api.Model;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
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

        IResult MarkAsRead(int playerNotificationId);
        // Oyuncuya ait bildirimi okundu olarak işaretle

        IResult SendNotificationToPlayers(List<int> playerIds, Notification notification);
        // Belirli kullanıcılara bildirim gönder
    }

}
