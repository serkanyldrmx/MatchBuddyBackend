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
        // Görünürlük (MatchVisibility): 0 herkese açık, 1 sadece davet / link. Kapalı maçlar başkalarının akışında görünmez.
        public byte Visibility { get; set; } = MatchVisibility.Public;
        // İşletme: uygulama rezervasyonunun ücreti tesiste tahsil edildi mi / ekip gelmedi mi (finans buna göre sayar)
        public bool IsPaid { get; set; }
        public bool NoShow { get; set; }
        // İşletme onaylı rezervasyonu iptal ettiyse (IsActive 3 + RejectReason) zamanı; null = normal red / iptal yok
        public DateTime? CancelledAt { get; set; }
        // Kurucu ücretsiz iptal süresi (VenueSettings.FreeCancellationHours) içinde maçtan çekildiyse zamanı ("geç iptal")
        public DateTime? LateCancelledAt { get; set; }
        public List<MatchTeam> MatchTeams { get; set; }
        public List<MatchComment> MatchComments { get; set; }
    }

    public static class MatchVisibility
    {
        public const byte Public = 0;
        public const byte InviteOnly = 1;

        public static bool IsValid(byte? value) => value is null or Public or InviteOnly;

        /// <summary>
        /// Kapalı maça erişim (akışta görme / katılma): kurucu, davet edilen (davet kaydı olan), kadroda kaydı olan
        /// ya da maçın ev / deplasman takımının üyesi. Açık maçta herkes.
        /// </summary>
        public static bool HasAccess(byte visibility, bool isCreator, bool isInvited, bool hasParticipantRecord, bool isTeamMember) =>
            visibility != InviteOnly || isCreator || isInvited || hasParticipantRecord || isTeamMember;

        /// <summary>Katılma kuralı: kapalı maça erişimi olmayanlar yalnızca davet linkiyle (ShareCode) katılabilir.</summary>
        public static bool CanJoin(byte visibility, bool viaInviteLink, bool isCreator, bool isInvited, bool hasParticipantRecord, bool isTeamMember) =>
            viaInviteLink || HasAccess(visibility, isCreator, isInvited, hasParticipantRecord, isTeamMember);
    }
}
