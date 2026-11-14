namespace MatcBuddy.Api.Model
{
    public class MatchStatusModel
    {
        public int MatchId { get; set; }
        public byte Status { get; set; }
        // Red sebebi (status = 3 iken Match.RejectReason'a yazılır, ≤ 200)
        public string? Reason { get; set; }
    }
}
