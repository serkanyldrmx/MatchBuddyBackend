using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    // Medya (fotoğraf) altyapısı (docs/media-api.md)

    /// <summary>Media.Kind değerleri.</summary>
    public enum MediaKind : byte
    {
        Avatar = 1,
        VenuePhoto = 2,
        MessageImage = 3,
    }

    /// <summary>Media.Status değerleri.</summary>
    public enum MediaStatus : byte
    {
        Pending = 0,   // yüklendi, henüz bir kayda bağlanmadı
        Attached = 1,
        Deleted = 2,
    }

    /// <summary>
    /// İşlenmiş (WebP sm/md/lg) bir fotoğraf. Dosyalar depoda "{KeyPrefix}/{sm|md|lg}.webp" anahtarlarındadır.
    /// </summary>
    public class Media : IEntity
    {
        public int MediaId { get; set; }
        public MediaKind Kind { get; set; }
        /// <summary>ör. "avatars/2026/11/3f..9a" (unique)</summary>
        public string KeyPrefix { get; set; } = string.Empty;
        /// <summary>Yükleyen (admin işletme adına yüklediyse admin).</summary>
        public int? OwnerPlayerId { get; set; }
        /// <summary>md varyantının boyutu</summary>
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>Tüm varyantların toplam bayt sayısı</summary>
        public int Bytes { get; set; }
        public MediaStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AttachedAt { get; set; }
    }
}
