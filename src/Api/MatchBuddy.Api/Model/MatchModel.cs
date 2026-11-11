using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class MatchModel
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int UserCount { get; set; }
        public string? Description { get; set; }
        public byte IsActive { get; set; }
        public int StadiumId { get; set; }

        // Anlaşmalı tesis rezervasyonu (docs/venues-api.md). StadiumId / IsActive artık SaveMatch'te kullanılmaz.
        public int? PitchId { get; set; }
        public int? DurationMinutes { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
        public int? HomeTeamId { get; set; }
        public int? AwayTeamId { get; set; }
        public bool LookingForOpponent { get; set; }
        /// <summary>En düşük güvenilirlik puanı (null = sınır yok)</summary>
        public int? MinReliability { get; set; }
        public bool RepeatWeekly { get; set; }
        //public Stadium Stadium { get; set; }
    }
}
