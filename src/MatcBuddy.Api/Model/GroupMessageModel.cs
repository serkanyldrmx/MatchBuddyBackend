namespace MatcBuddy.Api.Model
{
    public class GroupMessageModel
    {
        public int GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public string? UserName { get; set; }
        public byte Status { get; set; }
        // Media/Upload (kind=message) ile yüklenmiş, gönderene ait Pending medya
        public int? MediaId { get; set; }
        // Yanıtlanan grup mesajı (aynı gruptan)
        public int? ReplyToGroupMessageId { get; set; }
    }
}
