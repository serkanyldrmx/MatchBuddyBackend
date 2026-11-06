namespace MatchBuddy.Entities.DTOs
{
    // docs/tournaments-api.md "DTO'lar"

    public class TournamentStadiumDto
    {
        public int StadiumId { get; set; }
        public string? StadiumName { get; set; }
    }

    public class TournamentSummaryDto
    {
        public int TournamentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte Format { get; set; }
        public byte Status { get; set; }
        public DateTime StartDate { get; set; }
        public int MaxTeams { get; set; }
        public int TeamCount { get; set; }
        public TournamentStadiumDto? Stadium { get; set; }
        public PlayerSummaryDto? CreatedBy { get; set; }
        public TeamRefDto? Winner { get; set; }
        public List<int> MyTeamIds { get; set; } = new();
    }

    public class TournamentDetailDto : TournamentSummaryDto
    {
        public string? Description { get; set; }
        public bool CanManage { get; set; }
        public List<TournamentTeamDto> Teams { get; set; } = new();
        public List<TournamentFixtureDto> Fixtures { get; set; } = new();
        public List<StandingRowDto> Standings { get; set; } = new();
        public int RoundCount { get; set; }
        public List<TeamRefDto> MyEligibleTeams { get; set; } = new();
    }

    public class TournamentTeamDto
    {
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int MemberCount { get; set; }
        public List<PlayerSummaryDto> Members { get; set; } = new();
        public bool IsMine { get; set; }
    }

    public class TournamentFixtureDto
    {
        public int FixtureId { get; set; }
        public int Round { get; set; }
        public int Position { get; set; }
        public TeamRefDto? HomeTeam { get; set; }
        public TeamRefDto? AwayTeam { get; set; }
        public DateTime? MatchDate { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? WinnerTeamId { get; set; }
        public byte Status { get; set; }
    }

    public class StandingRowDto
    {
        public TeamRefDto Team { get; set; } = new();
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDiff { get; set; }
        public int Points { get; set; }
    }

    /// <summary>Create isteği (doğrulanmış).</summary>
    public class TournamentCreateInput
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public byte Format { get; set; }
        public DateTime? StartDate { get; set; }
        public int MaxTeams { get; set; }
        public int? StadiumId { get; set; }
    }
}
