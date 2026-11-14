using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class Match : IEntity
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int UserCount { get; set; }
        public string? Description { get; set; }
        public byte IsActive { get; set; }
        public int Likes { get; set; }
        public int StadiumId { get; set; }
        public Stadium Stadium { get; set; }

        // Maçı oluşturan oyuncu (eski maçlarda null)
        public int? CreatedByPlayerId { get; set; }

        // Sonuç girilince dolar
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public DateTime? ResultEnteredAt { get; set; }

        // Rezervasyon (docs/venues-api.md). IsActive: 1 bekliyor, 2 onaylı, 3 reddedildi
        public int? PitchId { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public decimal? PitchPrice { get; set; }
        public decimal? ExtrasPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
        public int? HomeTeamId { get; set; }
        public int? AwayTeamId { get; set; }
        public bool LookingForOpponent { get; set; }
        public string? RejectReason { get; set; }
        // Kurucunun istediği en düşük güvenilirlik puanı (null = sınır yok). Yeni oyuncular takılmaz.
        public int? MinReliability { get; set; }
        // Maç sonrası güvenilirlik olayları (Attended) yazıldı mı
        public DateTime? ReliabilitySettledAt { get; set; }
        // Haftalık sabit maç serisi (MatchSeries); null = tek seferlik
        public int? SeriesId { get; set; }
        // Oluşturulma zamanı (DB varsayılanı GETDATE())
        public DateTime CreatedAt { get; set; }
        // Paylaşılabilir davet linki kodu (/m/{kod}); ilk paylaşımda oluşur (docs/engagement-api.md)
        public string? ShareCode { get; set; }
        public List<MatchTeam> MatchTeams { get; set; }
        public List<MatchComment> MatchComments { get; set; }
    }
}
