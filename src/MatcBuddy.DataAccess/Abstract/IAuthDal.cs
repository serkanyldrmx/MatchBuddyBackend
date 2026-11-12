using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    /// <summary>
    /// Sosyal giriş hesap eşleştirmesinin ihtiyaç duyduğu dar arayüz (birim testlerde sahte uygulanır).
    /// </summary>
    public interface IExternalAccountStore
    {
        ExternalLogin? FindLink(string provider, string providerKey);
        void TouchLink(int externalLoginId, DateTime nowUtc);
        void RemoveLinkById(int externalLoginId);
        Player? FindPlayer(int playerId);
        /// <summary>Silinmemiş, e-postası (büyük/küçük harf duyarsız) eşleşen oyuncular.</summary>
        List<Player> FindPlayersByEmail(string email);
        bool HasLink(int playerId, string provider);
        bool UserNameExists(string userName);
        void AddLink(ExternalLogin link);
        /// <summary>Oyuncuyu ve bağlantıyı tek işlemde ekler; player.PlayerId doldurulur.</summary>
        void CreatePlayerWithLink(Player player, ExternalLogin link);
    }

    /// <summary>Oturum, tek kullanımlık kod, sosyal giriş ve veri dışa aktarımı (docs/auth-api.md).</summary>
    public interface IAuthDal : IExternalAccountStore
    {
        // ---- Refresh token
        void AddRefreshToken(RefreshToken token);
        RefreshToken? FindRefreshToken(string tokenHash);
        RefreshToken? GetRefreshToken(long id);
        /// <summary>Eski token hâlâ aktifse iptal eder ve yenisini ekler (tek işlem). Eski token zaten iptalse false.</summary>
        bool TryRotate(long oldId, RefreshToken replacement, DateTime nowUtc);
        bool Revoke(long id, string reason, DateTime nowUtc);
        int RevokeFamily(Guid familyId, string reason, DateTime nowUtc);
        int RevokeAllForPlayer(int playerId, string reason, DateTime nowUtc);

        // ---- Oyuncu
        Player? FindByUserName(string userName);
        /// <summary>Telefonun son 10 hanesi eşleşen, silinmemiş oyuncular.</summary>
        List<Player> FindPlayersByPhone(string last10Digits);
        void SetPassword(int playerId, string passwordHash);
        /// <summary>E-posta hâlâ <paramref name="email"/> ise doğrulandı olarak işaretler.</summary>
        bool SetEmailConfirmed(int playerId, string email);
        bool SetPhoneConfirmed(int playerId, string phone);
        void SetConsent(int playerId, string termsVersion, bool marketingConsent, DateTime nowUtc);
        void SetAvatar(int playerId, string url, int? mediaId);

        // ---- Kodlar
        AuthCode? LatestCode(int playerId, byte purpose);
        /// <summary>Oyuncunun bu amaçtaki kullanılmamış kodlarını geçersiz kılar, yenisini ekler.</summary>
        void ReplaceCode(AuthCode code);
        /// <summary>Deneme hakkı varsa sayacı atomik olarak artırır. Hak bitmişse/kod kullanılmışsa false.</summary>
        bool TryUseAttempt(long authCodeId, int maxAttempts);
        bool ConsumeCode(long authCodeId, DateTime nowUtc);
        void InvalidateCodes(int playerId, byte purpose, DateTime nowUtc);

        // ---- Sosyal giriş bağlantıları
        List<ExternalLogin> GetLinks(int playerId);
        bool RemoveLink(int playerId, string provider);

        // ---- KVKK
        /// <summary>Oyuncunun kendi verileri (JSON'a çevrilecek iç içe sözlük/liste).</summary>
        Dictionary<string, object?> Export(int playerId);
    }
}
