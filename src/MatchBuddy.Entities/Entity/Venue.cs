using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    // Anlaşmalı tesis paketi tabloları (docs/venues-api.md)

    /// <summary>Players.Role değerleri. IsAdmin her zaman Role == Admin ile senkron tutulur.</summary>
    public static class PlayerRoles
    {
        public const byte Player = 0;
        public const byte Admin = 1;
        public const byte Owner = 2;
    }

    public class StadiumPhoto : IEntity
    {
        public int PhotoId { get; set; }
        public int StadiumId { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsCover { get; set; }
        public int SortOrder { get; set; }
        // Url md URL'sini tutar; eski /uploads kayıtlarında null
        public int? MediaId { get; set; }
    }

    /// <summary>Tesisteki saha. Surface: 1 suni çim, 2 doğal çim, 3 parke, 4 diğer.</summary>
    public class Pitch : IEntity
    {
        public int PitchId { get; set; }
        public int StadiumId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte Surface { get; set; }
        public bool IsIndoor { get; set; }
        public int PlayersPerSide { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>Haftalık açılış saatleri. DayOfWeek: 0 Pazartesi … 6 Pazar. CloseTime &lt;= OpenTime ise ertesi güne taşar.</summary>
    public class OpeningHour : IEntity
    {
        public int OpeningHourId { get; set; }
        public int StadiumId { get; set; }
        public byte DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
    }

    /// <summary>Saatlik fiyat kuralı. PitchId null = tüm sahalar. DaysMask bit 0 = Pazartesi. ToHour hariç.</summary>
    public class PriceRule : IEntity
    {
        public int PriceRuleId { get; set; }
        public int StadiumId { get; set; }
        public int? PitchId { get; set; }
        public int DaysMask { get; set; }
        public int FromHour { get; set; }
        public int ToHour { get; set; }
        public decimal PricePerHour { get; set; }
    }

    /// <summary>Kampanya: DiscountPercent ya da FixedPrice (saatlik) — tam olarak biri dolu.</summary>
    public class Campaign : IEntity
    {
        public int CampaignId { get; set; }
        public int StadiumId { get; set; }
        public int? PitchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysMask { get; set; }
        public int FromHour { get; set; }
        public int ToHour { get; set; }
        public int? DiscountPercent { get; set; }
        public decimal? FixedPrice { get; set; }
        public bool IsActive { get; set; }
        // Boş saat fırsatı: tek bir boş saate indirim + oyunculara bildirim (docs/venue-growth-api.md)
        public bool IsDeal { get; set; }
        public DateTime? DealSentAt { get; set; }
        public int DealNotifiedCount { get; set; }
    }

    /// <summary>
    /// İşletmenin elle kapattığı aralık (Kind 0) ya da uygulama dışından aldığı harici rezervasyon (Kind 1).
    /// Kind 1'de Reason opsiyoneldir (boş metin saklanır), CustomerName ve Price zorunludur.
    /// </summary>
    public class SlotBlock : IEntity
    {
        public int SlotBlockId { get; set; }
        public int PitchId { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public byte Kind { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? Price { get; set; }
        public bool IsPaid { get; set; }
        public string? Note { get; set; }
        // Haftalık tekrar eden telefon rezervasyonu: aynı serideki kayıtlar
        public Guid? SeriesId { get; set; }
    }

    public static class SlotBlockKind
    {
        public const byte Closed = 0;
        public const byte Manual = 1;
    }

    /// <summary>Tesis gideri (docs/venues-api.md "Ek paket" §2).</summary>
    public class VenueExpense : IEntity
    {
        public int ExpenseId { get; set; }
        public int StadiumId { get; set; }
        public DateTime Date { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Gider kategorileri (sabit liste; frontend de aynı listeyi kullanır).</summary>
    public static class ExpenseCategories
    {
        public static readonly string[] All =
        {
            "Personel", "Kira", "Elektrik", "Su", "Doğalgaz", "Bakım-onarım", "Malzeme", "Vergi", "Reklam", "Diğer",
        };
    }

    /// <summary>Tesis yorumu. Unique (MatchId, PlayerId).</summary>
    public class VenueReview : IEntity
    {
        public int ReviewId { get; set; }
        public int StadiumId { get; set; }
        public int PlayerId { get; set; }
        public int MatchId { get; set; }
        public byte Rating { get; set; }
        public string? Comment { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class MatchStatus
    {
        public const byte Pending = 1;
        public const byte Approved = 2;
        public const byte Rejected = 3;
    }
}
