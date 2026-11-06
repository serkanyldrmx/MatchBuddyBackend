namespace MatchBuddy.Entities.DTOs
{
    // docs/social-api.md DTO'ları. JSON camelCase (IFollow -> "iFollow").

    /// <summary>PublicPlayerDto alanları + ilişki bilgileri ("ben"e göre).</summary>
    public class SocialPlayerDto : PlayerSummaryDto
    {
        public bool IFollow { get; set; }
        public bool FollowsMe { get; set; }
        public bool IsMutual { get; set; }
        public int PlayedTogether { get; set; }
    }

    public class TeamRefDto
    {
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
    }

    public class PlayerProfileDto
    {
        public PlayerSummaryDto Player { get; set; } = null!;
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IFollow { get; set; }
        public bool FollowsMe { get; set; }
        public bool IsMutual { get; set; }
        public int PlayedTogether { get; set; }
        public bool CanMessage { get; set; }
        public bool IsBlockedByMe { get; set; }
        public bool IsMe { get; set; }
        public bool StatsVisible { get; set; }
        public PlayerStatsDto? Stats { get; set; }
        public List<TeamRefDto>? Teams { get; set; }
    }

    public class FollowResultDto
    {
        public bool IsMutual { get; set; }
    }

    /// <summary>PlayerSettings alanları (PlayerId hariç).</summary>
    public class PlayerSettingsDto
    {
        public byte MessagePermission { get; set; }
        public byte ProfileVisibility { get; set; }
        public bool NotifyFollow { get; set; }
        public bool NotifyMatchInvite { get; set; }
        public bool NotifyTeamInvite { get; set; }
        public bool NotifyMatchReminder { get; set; }
        public bool NotifyMatchResult { get; set; }
        public bool NotifyMatchUpdates { get; set; }
    }

    /// <summary>POST Settings gövdesi: gönderilmeyen alanlar mevcut değerini korur.</summary>
    public class PlayerSettingsInput
    {
        public int? MessagePermission { get; set; }
        public int? ProfileVisibility { get; set; }
        public bool? NotifyFollow { get; set; }
        public bool? NotifyMatchInvite { get; set; }
        public bool? NotifyTeamInvite { get; set; }
        public bool? NotifyMatchReminder { get; set; }
        public bool? NotifyMatchResult { get; set; }
        public bool? NotifyMatchUpdates { get; set; }
    }

    public class LastMessageDto
    {
        public string? Text { get; set; }
        public DateTime SendingDate { get; set; }
        public bool Mine { get; set; }
    }

    public class MessageContactDto
    {
        public SocialPlayerDto Player { get; set; } = null!;
        public LastMessageDto? LastMessage { get; set; }
    }

    public class ActivityMatchDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public string? StadiumName { get; set; }
    }

    public class ActivityDto
    {
        public int ActivityId { get; set; }
        public byte Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlayerSummaryDto Actor { get; set; } = null!;
        public ActivityMatchDto? Match { get; set; }
        public TeamRefDto? Team { get; set; }
        public PlayerSummaryDto? Target { get; set; }
        public int? Value { get; set; }
    }

    public class AdminReportDto
    {
        public int ReportId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlayerSummaryDto Reporter { get; set; } = null!;
        public PlayerSummaryDto Reported { get; set; } = null!;
        public int ReportedCount { get; set; }
    }

    // ---- Davetler ----
    public class InviteMatchInfoDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public string? StadiumName { get; set; }
        public int Capacity { get; set; }
        public int GoingCount { get; set; }
        public byte IsActive { get; set; }
    }

    public class MatchInviteDto
    {
        public int InviteId { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlayerSummaryDto Inviter { get; set; } = null!;
        public InviteMatchInfoDto Match { get; set; } = null!;
    }

    public class InviteTeamInfoDto
    {
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int MemberCount { get; set; }
    }

    public class TeamInviteDto
    {
        public int InviteId { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlayerSummaryDto Inviter { get; set; } = null!;
        public InviteTeamInfoDto Team { get; set; } = null!;
    }

    public class MyInvitesDto
    {
        public List<MatchInviteDto> MatchInvites { get; set; } = new();
        public List<TeamInviteDto> TeamInvites { get; set; } = new();
    }

    public class InviteResultDto
    {
        public int Invited { get; set; }
        public int Skipped { get; set; }
    }

    // ---- Takvim ----
    public class CalendarItemDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public string? StadiumName { get; set; }
        public string Kind { get; set; } = "going"; // going | waitlist | invited
        public byte IsActive { get; set; }
        public int Capacity { get; set; }
        public int GoingCount { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}
