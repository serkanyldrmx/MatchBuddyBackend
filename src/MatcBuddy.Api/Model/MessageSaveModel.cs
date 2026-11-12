namespace MatcBuddy.Api.Model
{
    public class MessageSaveModel
    {
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public byte Status { get; set; }
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
        // Media/Upload (kind=message) ile yüklenmiş, gönderene ait Pending medya
        public int? MediaId { get; set; }
        // Yanıtlanan mesaj (aynı konuşmadan)
        public int? ReplyToMessageId { get; set; }
    }
}
