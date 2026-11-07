using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Telefona / tarayıcıya bildirim (Web Push) aboneliği — docs/push-api.md.
    /// Bir oyuncunun her cihazı (tarayıcı) ayrı bir abonelik; Endpoint tekildir.
    /// </summary>
    public class PushSubscription : IEntity
    {
        public int PushSubscriptionId { get; set; }
        public int PlayerId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
