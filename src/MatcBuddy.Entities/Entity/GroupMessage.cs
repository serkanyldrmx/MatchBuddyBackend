using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class GroupMessage : IEntity
    {
        public int GroupMessageId { get; set; }
        public int GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public byte Status { get; set; }
        // Resimli mesaj: medya ve md URL'si (yoksa null)
        public int? MediaId { get; set; }
        public string? ImageUrl { get; set; }
        // Yanıtlanan grup mesajı (aynı gruptan)
        public int? ReplyToGroupMessageId { get; set; }
        // Gönderen sildi: metin / resim boşaltılır, "Bu mesaj silindi" gösterilir
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Group Group { get; set; }
    }
}
