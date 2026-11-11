namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Oturum, sosyal giriş, şifre sıfırlama, e-posta doğrulama ve onay (KVKK) kodları — docs/auth-api.md.</summary>
        public static partial class Auth
        {
            // Oturum (refresh token)
            public const string RefreshInvalid = "AUTH_REFRESH_INVALID";                 // 401: çerez yok / geçersiz / süresi dolmuş / iptal
            public const string RefreshReused = "AUTH_REFRESH_REUSED";                   // 401: kullanılmış token tekrar geldi → aile iptal

            // Şifre politikası
            public const string PasswordTooWeak = "AUTH_PASSWORD_TOO_WEAK";             // en az bir harf ve bir rakam
            public const string PasswordNotSet = "AUTH_PASSWORD_NOT_SET";               // sosyal hesapta şifre yok (ChangePassword)

            // Tek kullanımlık kodlar (sıfırlama / e-posta doğrulama)
            public const string IdentifierRequired = "AUTH_IDENTIFIER_REQUIRED";
            public const string InvalidChannel = "AUTH_INVALID_CHANNEL";
            public const string CodeRequired = "AUTH_CODE_REQUIRED";
            public const string CodeInvalid = "AUTH_CODE_INVALID";                       // yanlış / süresi dolmuş / yok
            public const string CodeAttemptsExceeded = "AUTH_CODE_ATTEMPTS_EXCEEDED";    // çok fazla yanlış deneme
            public const string CodeResendCooldown = "AUTH_CODE_RESEND_COOLDOWN";        // {0} saniye (429)

            // E-posta doğrulama
            public const string EmailMissing = "AUTH_EMAIL_MISSING";
            public const string EmailAlreadyConfirmed = "AUTH_EMAIL_ALREADY_CONFIRMED";

            // Sosyal giriş
            public const string ProviderNotConfigured = "AUTH_PROVIDER_NOT_CONFIGURED";  // {0} sağlayıcı
            public const string ProviderUnknown = "AUTH_PROVIDER_UNKNOWN";
            public const string ProviderUnavailable = "AUTH_PROVIDER_UNAVAILABLE";       // 502
            public const string ExternalTokenRequired = "AUTH_EXTERNAL_TOKEN_REQUIRED";
            public const string ExternalTokenInvalid = "AUTH_EXTERNAL_TOKEN_INVALID";    // 401
            public const string ExternalLoginNotFound = "AUTH_EXTERNAL_LOGIN_NOT_FOUND"; // 404
            public const string ExternalLoginLastMethod = "AUTH_EXTERNAL_LOGIN_LAST_METHOD";

            // KVKK / onay
            public const string TermsRequired = "AUTH_TERMS_REQUIRED";                   // {0} güncel sürüm
            public const string TermsVersionMismatch = "AUTH_TERMS_VERSION_MISMATCH";    // {0} güncel sürüm
        }
    }
}
