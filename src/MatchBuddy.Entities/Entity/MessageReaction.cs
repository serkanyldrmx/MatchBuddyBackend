using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Mesaja tepki (emoji). Birebir mesajda MessageId, grup mesajında GroupMessageId dolu.
    /// Oyuncu başına mesaj başına tek tepki: aynı emoji kaldırır, farklı emoji değiştirir.
    /// </summary>
    public class MessageReaction : IEntity
    {
        public int ReactionId { get; set; }
        public int? MessageId { get; set; }
        public int? GroupMessageId { get; set; }
        public int PlayerId { get; set; }
        public string Emoji { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
