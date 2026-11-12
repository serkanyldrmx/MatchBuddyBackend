using MatcBuddy.Api.Model;
using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface INotificationDal : IEntityRepository<Notification>
    {
        // Alıcı tercihlerine uyarak bildirim gönderir; satır oluşturulan alıcı sayısını döner
        int SendToPlayers(NotificationType type, string text, int? matchId, int? stadiumId, List<int> playerIds);
    }
}
