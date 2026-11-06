using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.Api.Model
{
    // Join / Leave / Confirm / ToggleLike gövdesi
    public class MatchIdModel
    {
        public int MatchId { get; set; }
    }

    public class SetSideModel
    {
        public int MatchId { get; set; }
        public int PlayerId { get; set; }
        public byte Side { get; set; }
    }

    // Misafir oyuncular
    public class AddGuestModel
    {
        public int MatchId { get; set; }
        public byte Side { get; set; }
        public string? Name { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateGuestModel
    {
        public int GuestId { get; set; }
        public string? Name { get; set; }
        public string? Note { get; set; }
        public byte Side { get; set; }
    }

    public class GuestIdModel
    {
        public int GuestId { get; set; }
    }

    public class SubmitResultModel
    {
        public int MatchId { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public List<PlayerStatInput>? Stats { get; set; }
    }

    public class VoteMvpModel
    {
        public int MatchId { get; set; }
        public int VotedPlayerId { get; set; }
    }

    public class RatePlayersModel
    {
        public int MatchId { get; set; }
        public List<PlayerRatingInput>? Ratings { get; set; }
    }

    public class JoinResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public byte Status { get; set; }
    }
}
