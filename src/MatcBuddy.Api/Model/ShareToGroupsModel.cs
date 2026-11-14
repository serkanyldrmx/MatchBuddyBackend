namespace MatcBuddy.Api.Model
{
    /// <summary>Maçı gruplara gönder: { matchId, groupIds: [..], note? }</summary>
    public class ShareToGroupsModel
    {
        public int MatchId { get; set; }
        public List<int>? GroupIds { get; set; }
        public string? Note { get; set; }
    }
}
