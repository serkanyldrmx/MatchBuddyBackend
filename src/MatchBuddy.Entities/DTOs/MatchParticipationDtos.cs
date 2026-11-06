using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Entities.DTOs
{
    /// <summary>
    /// Herkese gösterilebilen oyuncu özeti (şifre / iletişim bilgisi yok).
    /// Api katmanındaki PublicPlayerDto bu sınıftan türer; JSON şekli aynıdır.
    /// </summary>
    public class PlayerSummaryDto
    {
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public int Age { get; set; }
        public byte IsAdmin { get; set; }
        // 0 oyuncu, 1 admin, 2 işletme (docs/venues-api.md)
        public byte Role { get; set; }
        public int UserScore { get; set; }
        public string? ProfilePictureUrl { get; set; }

        public static PlayerSummaryDto FromPlayer(Player p) => new()
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            PlayerSurname = p.PlayerSurname,
            UserName = p.UserName,
            Size = p.Size,
            Weight = p.Weight,
            Age = p.Age,
            IsAdmin = p.IsAdmin,
            Role = p.Role,
            UserScore = p.UserScore,
            ProfilePictureUrl = p.ProfilePictureUrl,
        };
    }

    /// <summary>GetMatchFeed ve GetMatchDetail'e eklenen tesis / rezervasyon alanları (docs/venues-api.md).</summary>
    public class MatchVenueFieldsDto
    {
        public MatchVenueDto? Venue { get; set; }
        public int DurationMinutes { get; set; }
        public decimal? PitchPrice { get; set; }
        public decimal? ExtrasPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? PerPlayer { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
        public TeamRefDto? HomeTeam { get; set; }
        public TeamRefDto? AwayTeam { get; set; }
        public bool LookingForOpponent { get; set; }
        public string? RejectReason { get; set; }
    }

    // ---- GET GetMatchFeed ----
    public class MatchFeedItemDto : MatchVenueFieldsDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public string? Description { get; set; }
        public byte IsActive { get; set; }
        public int Capacity { get; set; }
        public int GoingCount { get; set; }
        public int WaitlistCount { get; set; }
        public int Likes { get; set; }
        public bool LikedByMe { get; set; }
        public byte? MyStatus { get; set; }
        public int StadiumId { get; set; }
        public string? StadiumName { get; set; }
        public string? StadiumAddress { get; set; }
        public PlayerSummaryDto? CreatedBy { get; set; }
        public List<PlayerSummaryDto> ParticipantsPreview { get; set; } = new();
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public PlayerSummaryDto? Mvp { get; set; }

        // Sosyal paket: maçta Going olan, benim takip ettiklerim
        public int FollowingGoingCount { get; set; }
        public List<PlayerSummaryDto> FollowingPreview { get; set; } = new();
    }

    // ---- GET GetMatchDetail ----
    public class MatchDetailDto : MatchVenueFieldsDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public string? Description { get; set; }
        public byte IsActive { get; set; }
        public int Capacity { get; set; }
        public int Likes { get; set; }
        public bool LikedByMe { get; set; }
        public MatchStadiumDto? Stadium { get; set; }
        public PlayerSummaryDto? CreatedBy { get; set; }
        public bool CanManage { get; set; }
        public bool IsPast { get; set; }
        public byte? MyStatus { get; set; }
        public bool MyConfirmed { get; set; }
        public byte? MySide { get; set; }
        public List<MatchParticipantDto> Participants { get; set; } = new();
        public MatchResultDto? Result { get; set; }
        public MatchMvpDto? Mvp { get; set; }
        public int? MyMvpVote { get; set; }
        public List<MyRatingDto> MyRatings { get; set; } = new();

        // Tesis paketi: işletmeye mesaj, tesis yorumu
        public int? OwnerPlayerId { get; set; }
        public bool CanReview { get; set; }
        public MyReviewDto? MyReview { get; set; }
    }

    public class MatchStadiumDto
    {
        public int StadiumId { get; set; }
        public string? StadiumName { get; set; }
        public string? Address { get; set; }
        public string? Location { get; set; }
        // "HH:mm:ss" (net7 System.Text.Json TimeOnly'yi desteklemediği için string)
        public string? OpeningTime { get; set; }
        public string? ClosingTime { get; set; }
    }

    public class MatchParticipantDto
    {
        public PlayerSummaryDto Player { get; set; } = null!;
        public byte Status { get; set; }
        public byte? Side { get; set; }
        public bool Confirmed { get; set; }
        public DateTime JoinedAt { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int MvpVotes { get; set; }
        public double? AvgRating { get; set; }
        // Sosyal paket: bu oyuncuyu takip ediyor muyum
        public bool IFollow { get; set; }
    }

    public class MatchResultDto
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public DateTime? EnteredAt { get; set; }
    }

    public class MatchMvpDto
    {
        public PlayerSummaryDto Player { get; set; } = null!;
        public int Votes { get; set; }
    }

    public class MyRatingDto
    {
        public int PlayerId { get; set; }
        public int Score { get; set; }
    }

    // ---- POST istek gövdeleri (iş katmanına giden) ----
    public class PlayerStatInput
    {
        public int PlayerId { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
    }

    public class PlayerRatingInput
    {
        public int PlayerId { get; set; }
        public int Score { get; set; }
    }

    public class ToggleLikeResultDto
    {
        public bool Liked { get; set; }
        public int Likes { get; set; }
    }

    // ---- Oyuncu istatistikleri ----
    public class PlayerStatsDto
    {
        public int PlayerId { get; set; }
        public int MatchesPlayed { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int MvpCount { get; set; }
        public double? AvgRating { get; set; }
        public int RatingCount { get; set; }
        public int UserScore { get; set; }
        public List<PlayerRecentMatchDto> RecentMatches { get; set; } = new();
    }

    public class PlayerRecentMatchDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public byte? Side { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public bool WasMvp { get; set; }
    }

    public class LeaderboardRowDto
    {
        public PlayerSummaryDto Player { get; set; } = null!;
        public int MatchesPlayed { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int MvpCount { get; set; }
        public double? AvgRating { get; set; }
        public int UserScore { get; set; }
    }
}
