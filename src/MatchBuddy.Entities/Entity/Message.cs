using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Message : IEntity
    {
        public int MessageId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public byte Status { get; set; }
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
    }
}
