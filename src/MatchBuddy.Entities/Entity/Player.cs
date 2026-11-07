using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    public class Player : IEntity
    {
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public int Age { get; set; }
        public byte IsAdmin { get; set; }
        public int UserScore { get; set; }
        public string? ProfilePictureUrl { get; set; }
        // Profil fotoğrafı medyası (ProfilePictureUrl md URL'sini tutar). Eski /uploads kayıtlarında null.
        public int? ProfileMediaId { get; set; }

        // Hesap silindi (anonimleştirildi): hiçbir listede görünmez, giriş yapamaz
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // 0 oyuncu, 1 admin, 2 işletme (PlayerRoles). IsAdmin == 1 <=> Role == 1
        public byte Role { get; set; }
        // Sadece işletmeler için: üyelik bitiş tarihi (null = süresiz) ve askı durumu
        public DateTime? MembershipUntil { get; set; }
        public bool MembershipSuspended { get; set; }

        // ---- Hesap güvenliği / KVKK (docs/auth-api.md). Tarihler UTC.
        // E-posta doğrulandı mı (kod ile, sosyal sağlayıcının doğruladığı e-posta ile ya da e-posta ile şifre sıfırlamada)
        public bool EmailConfirmed { get; set; }
        // Telefon doğrulandı mı (SMS ile şifre sıfırlamada işaretlenir)
        public bool PhoneConfirmed { get; set; }
        // Kullanılabilir bir şifre var mı (sosyal girişle açılan hesapta false; şifre sıfırlama ile true olur)
        public bool HasPassword { get; set; } = true;
        public DateTime? TermsAcceptedAt { get; set; }
        public string? TermsVersion { get; set; }
        public DateTime? PrivacyAcceptedAt { get; set; }
        public bool MarketingConsent { get; set; }
        public DateTime? MarketingConsentAt { get; set; }
        public List<MatchComment> MatchComments { get; set; }
        public List<PlayerTeam> PlayerTeams { get; set; }
        public List<GroupPlayer> GroupPlayers { get; set; }
        public List<PlayerNotification> PlayerNotifications { get; set; }

    }
}
