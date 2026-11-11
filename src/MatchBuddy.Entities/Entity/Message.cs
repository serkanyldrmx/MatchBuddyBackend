using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Message : IEntity
    {
        public int MessageId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public byte Status { get; set; }
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
        // Resimli mesaj: medya ve md URL'si (yoksa null)
        public int? MediaId { get; set; }
        public string? ImageUrl { get; set; }
        // Alıcının konuşmayı açtığı an (null = okunmadı)
        public DateTime? ReadAt { get; set; }
        // Yanıtlanan mesaj (aynı konuşmadan)
        public int? ReplyToMessageId { get; set; }
        // Gönderen sildi: metin / resim boşaltılır, "Bu mesaj silindi" gösterilir
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
