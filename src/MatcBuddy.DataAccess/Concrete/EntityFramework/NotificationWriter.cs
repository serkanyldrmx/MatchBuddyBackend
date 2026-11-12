using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Tüm bildirimler buradan oluşturulur: alıcının PlayerSettings tercihleri (docs/social-api.md
    /// eşleme tablosu) kontrol edilir, tercihi kapalı olan ya da silinmiş hesaplara satır yazılmaz.
    /// SaveChanges çağırmaz; çağıranın context'ine/transaction'ına eklenir.
    /// </summary>
    public static class NotificationWriter
    {
        // Tercih kapalıysa bildirim gönderilmez. null = bu tür için tercih yok (her zaman gönderilir).
        public static Func<PlayerSettings, bool>? PreferenceFor(NotificationType type) => type switch
        {
            NotificationType.NewFollower => s => s.NotifyFollow,
            NotificationType.MatchInvite => s => s.NotifyMatchInvite,
            NotificationType.TeamInvite => s => s.NotifyTeamInvite,
            NotificationType.MatchReminder => s => s.NotifyMatchReminder,
            NotificationType.MatchResult => s => s.NotifyMatchResult,
            NotificationType.SlotDeal => s => s.NotifyDeals,
            NotificationType.MatchJoin
                or NotificationType.MatchApproval
                or NotificationType.MatchRejection
                or NotificationType.MatchCreated
                or NotificationType.WaitlistPromoted
                or NotificationType.TournamentUpdate
                or NotificationType.ReservationRequest
                or NotificationType.VenueReview => s => s.NotifyMatchUpdates,
            _ => null,
        };

        /// <summary>Tercihlere göre süzülmüş alıcı listesi.</summary>
        public static List<int> FilterRecipients(MatcBuddyContext context, NotificationType type, IEnumerable<int> playerIds)
        {
            var ids = playerIds.Distinct().ToList();
            if (ids.Count == 0) return ids;

            // Silinmiş (ve var olmayan) hesaplar çıkarılır
            var active = context.Players.AsNoTracking()
                .Where(p => ids.Contains(p.PlayerId) && !p.IsDeleted)
                .Select(p => p.PlayerId)
                .ToHashSet();
            ids = ids.Where(active.Contains).ToList();

            var preference = PreferenceFor(type);
            if (preference == null || ids.Count == 0) return ids;

            // Kayıt yoksa varsayılan = açık
            var optedOut = context.PlayerSettings.AsNoTracking()
                .Where(s => ids.Contains(s.PlayerId))
                .ToList()
                .Where(s => !preference(s))
                .Select(s => s.PlayerId)
                .ToHashSet();
            return ids.Where(id => !optedOut.Contains(id)).ToList();
        }

        /// <summary>
        /// Tek Notification + her (tercihi açık) alıcı için PlayerNotification ekler.
        /// Alıcı kalmazsa hiçbir şey eklemez ve null döner.
        /// </summary>
        public static Notification? Add(MatcBuddyContext context, NotificationType type, string text,
            int? matchId, int? stadiumId, IEnumerable<int> playerIds)
        {
            var ids = FilterRecipients(context, type, playerIds);
            if (ids.Count == 0) return null;

            var now = DateTime.Now;
            var notification = new Notification
            {
                Text = text,
                Type = type,
                CreatedAt = now,
                IsRead = false,
                MatchId = matchId,
                StadiumId = stadiumId,
                PlayerNotifications = ids.Select(id => new PlayerNotification
                {
                    PlayerId = id,
                    IsRead = false,
                    SentAt = now
                }).ToList()
            };
            context.Notification.Add(notification);
            return notification;
        }
    }
}
