using MatchBuddy.Api.Model;
using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface INotificationDal : IEntityRepository<Notification>
    {
        // Alıcı tercihlerine uyarak bildirim gönderir; satır oluşturulan alıcı sayısını döner
        int SendToPlayers(NotificationType type, string text, int? matchId, int? stadiumId, List<int> playerIds);
    }
}
