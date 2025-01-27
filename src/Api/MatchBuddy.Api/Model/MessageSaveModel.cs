namespace MatchBuddy.Api.Model
{
    public class MessageSaveModel
    {
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public byte Status { get; set; }
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
    }
}
