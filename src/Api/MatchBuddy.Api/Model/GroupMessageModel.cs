namespace MatchBuddy.Api.Model
{
    public class GroupMessageModel
    {
        public int GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public string? UserName { get; set; }
        public byte Status { get; set; }
    }
}
