namespace MatcBuddy.Business.Auth
{
    /// <summary>
    /// Şifre politikası (parametreler): security.passwordMinLength + auth.passwordRequireLetterAndDigit.
    /// Kayıt, profil güncelleme, şifre değiştirme ve sıfırlamada aynı kural uygulanır.
    /// </summary>
    public static class PasswordPolicy
    {
        public static int MinLength => P.Int(ParameterKeys.Security.PasswordMinLength);
        public static bool RequireLetterAndDigit => P.Bool(ParameterKeys.Auth.PasswordRequireLetterAndDigit);

        /// <summary>Geçerliyse null, değilse hata (alan: newPassword / password).</summary>
        public static AppError? Validate(string? password)
        {
            if (string.IsNullOrEmpty(password)) return AppError.Bad(ErrorCodes.Auth.PasswordRequired);
            if (password.Length < MinLength) return AppError.Bad(ErrorCodes.Auth.PasswordTooShort, MinLength);
            if (RequireLetterAndDigit && (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)))
                return AppError.Bad(ErrorCodes.Auth.PasswordTooWeak);
            return null;
        }
    }

    /// <summary>Uygulama düzeyi kimlik ayarları (yapılandırmadan; gizli değerler parametre tablosunda tutulmaz).</summary>
    public sealed class AuthSettings
    {
        /// <summary>E-posta bağlantıları için frontend adresi (Auth:FrontendBaseUrl), ör. https://matcbuddy.com.</summary>
        public string FrontendBaseUrl { get; set; } = "http://localhost:3000";

        /// <summary>Kod özetleri için gizli anahtar (Jwt:Key'den türetilir).</summary>
        public string CodePepper { get; set; } = string.Empty;
    }
}
