using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class GroupMessage : IEntity
    {
        public int GroupMessageId { get; set; }
        public int GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public byte Status { get; set; }
        public Group Group { get; set; }
    }
}
