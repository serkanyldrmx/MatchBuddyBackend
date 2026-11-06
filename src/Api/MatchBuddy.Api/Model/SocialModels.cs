namespace MatchBuddy.Api.Model
{
    // ---- /api/Social istek gövdeleri ----
    public class PlayerIdModel
    {
        public int PlayerId { get; set; }
    }

    public class ReportModel
    {
        public int PlayerId { get; set; }
        public string? Reason { get; set; }
        public string? Details { get; set; }
    }

    public class DeleteAccountModel
    {
        public string? Password { get; set; }
    }

    // ---- /api/Invites ----
    public class InviteToMatchModel
    {
        public int MatchId { get; set; }
        public List<int>? PlayerIds { get; set; }
    }

    public class InviteToTeamModel
    {
        public int TeamId { get; set; }
        public List<int>? PlayerIds { get; set; }
    }

    public class RespondInviteModel
    {
        public string? Kind { get; set; } // "match" | "team"
        public int InviteId { get; set; }
        public bool Accept { get; set; }
    }

    // ---- /api/Admin ----
    public class ReportIdModel
    {
        public int ReportId { get; set; }
    }
}
