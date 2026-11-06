namespace MatchBuddy.Api.Model
{
    // ---- /api/Tournaments istek gövdeleri (docs/tournaments-api.md) ----
    public class CreateTournamentModel
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public byte Format { get; set; }
        public DateTime? StartDate { get; set; }
        public int MaxTeams { get; set; }
        public int? StadiumId { get; set; }
    }

    public class TournamentIdModel
    {
        public int TournamentId { get; set; }
    }

    public class TournamentTeamModel
    {
        public int TournamentId { get; set; }
        public int TeamId { get; set; }
    }

    public class SubmitFixtureResultModel
    {
        public int FixtureId { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? WinnerTeamId { get; set; }
    }

    public class UpdateFixtureDateModel
    {
        public int FixtureId { get; set; }
        public DateTime? MatchDate { get; set; }
    }
}
