using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    /// <summary>Admin → Kullanıcılar (arama, engel) ve şikayet sonuçlandırma.</summary>
    public interface IModerationDal
    {
        /// <summary>Silinmemiş hesaplarda ad / soyad / kullanıcı adı / e-posta araması; şikayet sayıları ile.</summary>
        PagedDto<AdminUserDto> SearchUsers(string? query, bool bannedOnly, int page, int pageSize, DateTime utcNow);
        /// <summary>Silinmemiş oyuncu (izlenmeden)</summary>
        Player? FindPlayer(int playerId);
        void SetBan(int playerId, bool banned, DateTime? untilUtc, string? reason);

        Report? FindReport(int reportId);
        void ResolveReport(int reportId, int adminId, string? note, byte action, DateTime utcNow);
    }

    /// <summary>İletişim formu mesajları.</summary>
    public interface IContactDal
    {
        void Add(ContactMessage message);
        /// <summary>Okunmamışlar önce, sonra en yeni.</summary>
        PagedDto<ContactMessageDto> List(bool? handled, int page, int pageSize);
        int UnhandledCount();
        bool SetHandled(int contactMessageId, bool handled, DateTime utcNow);
        /// <summary>Bildirim gidecek yönetici e-postaları</summary>
        List<(string Email, string Name)> AdminEmails();
    }
}
