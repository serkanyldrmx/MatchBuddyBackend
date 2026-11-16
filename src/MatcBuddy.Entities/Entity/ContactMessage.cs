using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    /// <summary>
    /// Herkese açık iletişim formundan gelen mesaj (POST /api/Contact). Yönetim panelinde "Mesajlar / İletişim"
    /// ekranında listelenir; giriş yapmış kullanıcı gönderdiyse PlayerId dolar. Tarihler UTC.
    /// </summary>
    public class ContactMessage : IEntity
    {
        public int ContactMessageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? PlayerId { get; set; }
        public bool IsHandled { get; set; }
        public DateTime? HandledAt { get; set; }
    }
}
