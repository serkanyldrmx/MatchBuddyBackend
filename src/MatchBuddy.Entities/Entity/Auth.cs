using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Yenileme (refresh) token'ı. Değerin kendisi saklanmaz, yalnızca SHA-256 özeti (TokenHash).
    /// Her yenilemede döndürülür (rotation); aynı oturum zinciri FamilyId ile bağlıdır. Tarihler UTC.
    /// </summary>
    public class RefreshToken : IEntity
    {
        public long RefreshTokenId { get; set; }
        public int PlayerId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public Guid FamilyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        /// <summary>"Beni hatırla": true → kalıcı çerez (auth.refreshTokenDays), false → oturum çerezi (auth.sessionRefreshTokenHours).</summary>
        public bool Persistent { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedReason { get; set; }
        public long? ReplacedById { get; set; }
        public string? CreatedByIp { get; set; }
        public string? UserAgent { get; set; }
    }

    /// <summary>Sosyal giriş bağlantısı (google / facebook). (Provider, ProviderKey) tekildir.</summary>
    public class ExternalLogin : IEntity
    {
        public int ExternalLoginId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ProviderKey { get; set; } = string.Empty;
        public int PlayerId { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>Tek kullanımlık 6 haneli kod (şifre sıfırlama / e-posta doğrulama). Kodun kendisi saklanmaz.</summary>
    public class AuthCode : IEntity
    {
        public long AuthCodeId { get; set; }
        public int PlayerId { get; set; }
        public byte Purpose { get; set; }
        public byte Channel { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        /// <summary>Kodun gönderildiği adres/telefon (doğrulama, adres değiştiyse geçersiz sayılır).</summary>
        public string Destination { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public string? CreatedByIp { get; set; }
    }

    public static class AuthCodePurposes
    {
        public const byte PasswordReset = 1;
        public const byte EmailVerification = 2;
    }

    public static class AuthChannels
    {
        public const byte Email = 1;
        public const byte Sms = 2;

        public static byte? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "email" or "e-mail" or "mail" => Email,
            "sms" or "phone" => Sms,
            _ => null,
        };
    }

    public static class ExternalProviders
    {
        public const string Google = "google";
        public const string Facebook = "facebook";
        public static readonly string[] All = { Google, Facebook };

        public static string? Normalize(string? value)
        {
            var v = value?.Trim().ToLowerInvariant();
            return v != null && All.Contains(v) ? v : null;
        }
    }

    public static class RevokeReasons
    {
        public const string Rotated = "rotated";
        public const string Logout = "logout";
        public const string LogoutAll = "logout_all";
        public const string ReuseDetected = "reuse_detected";
        public const string PasswordChanged = "password_changed";
        public const string PasswordReset = "password_reset";
        public const string AccountDeleted = "account_deleted";
    }
}
