namespace MatcBuddy.Entities.DTOs
{
    /// <summary>Oyuncu rozeti (katılımcı listeleri, profil). Level: new | good | medium | risky.</summary>
    public class ReliabilityBadgeDto
    {
        public int Score { get; set; }
        public string Level { get; set; } = "new";
        public int CompletedMatches { get; set; }
    }

    /// <summary>"Maça gelemiyorum" penceresindeki ceza önizlemesi. Kind: none | late | veryLate.</summary>
    public class LeavePreviewDto
    {
        public double HoursLeft { get; set; }
        public string Kind { get; set; } = "none";
        public int Penalty { get; set; }
        public int CurrentScore { get; set; }
        /// <summary>Yalnızca "katılıyor" durumundaki çekilme cezalıdır; yedek listeden çıkmak cezasızdır.</summary>
        public bool IsGoing { get; set; }
    }

    public class LeaveResultDto
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public int Penalty { get; set; }
        public int NewScore { get; set; }
    }

    public class ReliabilityHistoryItemDto
    {
        public int EventId { get; set; }
        public int? MatchId { get; set; }
        public string? MatchName { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Delta { get; set; }
        public string? ReasonCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Reversed { get; set; }
    }

    public class ReliabilitySummaryDto : ReliabilityBadgeDto
    {
        /// <summary>Son 10 işlenmiş maç ve iptaller.</summary>
        public int Attended { get; set; }
        public int LateCancels { get; set; }
        public int NoShows { get; set; }
        /// <summary>Yalnızca oyuncunun kendisi ve admin görür.</summary>
        public List<ReliabilityHistoryItemDto>? History { get; set; }
    }

    /// <summary>Kurucunun maç detayında gördüğü son dakika çekilmeler / gelmeyenler.</summary>
    public class MatchWithdrawalDto
    {
        public int EventId { get; set; }
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Penalty { get; set; }
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Excused { get; set; }
    }
}
