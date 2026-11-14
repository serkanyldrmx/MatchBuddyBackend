using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    /// <summary>Turnuva / lig (docs/tournaments-api.md).</summary>
    public class Tournament : IEntity
    {
        public int TournamentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte Format { get; set; }
        public byte Status { get; set; }
        public DateTime StartDate { get; set; }
        public int MaxTeams { get; set; }
        public int? StadiumId { get; set; }
        public Stadium? Stadium { get; set; }
        public int CreatedByPlayerId { get; set; }
        public Player CreatedByPlayer { get; set; }
        public int? WinnerTeamId { get; set; }
        public Team? WinnerTeam { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<TournamentTeam> Teams { get; set; } = new();
        public List<TournamentFixture> Fixtures { get; set; } = new();
    }

    /// <summary>Turnuvaya kayıtlı takım. Unique (TournamentId, TeamId).</summary>
    public class TournamentTeam : IEntity
    {
        public int TournamentTeamId { get; set; }
        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; }
        public int TeamId { get; set; }
        public Team Team { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>Fikstür satırı. Round 1'den, Position 0'dan başlar.</summary>
    public class TournamentFixture : IEntity
    {
        public int FixtureId { get; set; }
        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; }
        public int Round { get; set; }
        public int Position { get; set; }
        public int? HomeTeamId { get; set; }
        public Team? HomeTeam { get; set; }
        public int? AwayTeamId { get; set; }
        public Team? AwayTeam { get; set; }
        public DateTime? MatchDate { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? WinnerTeamId { get; set; }
        public Team? WinnerTeam { get; set; }
        public byte Status { get; set; }
    }

    public static class TournamentFormat
    {
        public const byte League = 1;
        public const byte Knockout = 2;
    }

    public static class TournamentStatus
    {
        public const byte Registration = 0;
        public const byte Ongoing = 1;
        public const byte Finished = 2;
    }

    public static class FixtureStatus
    {
        public const byte Scheduled = 0;
        public const byte Played = 1;
        public const byte Bye = 2;
    }
}
