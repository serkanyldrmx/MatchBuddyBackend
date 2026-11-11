using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Haftalık sabit maç ("her salı 21:00 aynı ekip") — docs/venue-growth-api.md.
    /// Her hafta gerçek bir Match (SeriesId) oluşturulur; NextDate bir sonraki oluşturulacak haftayı tutar.
    /// İşletme serinin bir maçını onayladıktan sonra yeni haftalar otomatik onaylı açılır.
    /// </summary>
    public class MatchSeries : IEntity
    {
        public int SeriesId { get; set; }
        public int CreatedByPlayerId { get; set; }
        public int StadiumId { get; set; }
        public int PitchId { get; set; }
        public DateTime NextDate { get; set; }
        public bool IsActive { get; set; }
        public bool Approved { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
    }
}
