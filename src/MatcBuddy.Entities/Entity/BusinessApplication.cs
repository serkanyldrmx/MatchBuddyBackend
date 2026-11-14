using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    /// <summary>
    /// İşletmeler sayfasından gelen üyelik başvurusu. İşletme hesabı kendiliğinden açılmaz:
    /// yönetici başvuranı arar, uygun görürse hesabı yönetim panelinden (İşletmeler) oluşturur.
    /// </summary>
    public class BusinessApplication : IEntity
    {
        public int BusinessApplicationId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public int? PitchCount { get; set; }
        public string? Note { get; set; }
        public byte Status { get; set; } = BusinessApplicationStatus.New;
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public static class BusinessApplicationStatus
    {
        public const byte New = 0;
        public const byte Contacted = 1;   // arandı / görüşülüyor
        public const byte Converted = 2;   // hesap açıldı
        public const byte Rejected = 3;
        public static bool IsValid(int s) => s is >= New and <= Rejected;
    }
}
