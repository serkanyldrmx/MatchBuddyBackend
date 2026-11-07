namespace MatchBuddy.Api.Model
{
    public class AuthLoginModel
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        /// <summary>"Beni hatırla": true → kalıcı çerez (auth.refreshTokenDays); false → oturum çerezi (auth.sessionRefreshTokenHours).</summary>
        public bool RememberMe { get; set; } = true;
    }

    public class GoogleLoginModel
    {
        /// <summary>Google Identity Services "credential" (id_token JWT).</summary>
        public string? IdToken { get; set; }
        public bool RememberMe { get; set; } = true;
    }

    public class FacebookLoginModel
    {
        /// <summary>Facebook Login (JS SDK) authResponse.accessToken.</summary>
        public string? AccessToken { get; set; }
        public bool RememberMe { get; set; } = true;
    }

    public class ChangePasswordModel
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class ForgotPasswordModel
    {
        /// <summary>Kullanıcı adı, e-posta ya da telefon.</summary>
        public string? Identifier { get; set; }
        /// <summary>"email" | "sms"</summary>
        public string? Channel { get; set; }
    }

    public class ResetPasswordModel
    {
        public string? Identifier { get; set; }
        public string? Code { get; set; }
        public string? NewPassword { get; set; }
    }

    public class VerifyEmailModel
    {
        public string? Code { get; set; }
    }

    public class AcceptTermsModel
    {
        public string? TermsVersion { get; set; }
        public bool MarketingConsent { get; set; }
    }

    /// <summary>LoginResponse ({ token, expiresAt, player, role }) + oturum/hesap durum alanları. docs/auth-api.md.</summary>
    public class AuthLoginResponse : LoginResponse
    {
        /// <summary>Refresh çerezinin bitişi (UTC). rememberMe=false iken çerez tarayıcı kapanınca da silinir.</summary>
        /// <summary>null: eş zamanlı yenileme toleransında (çerez değişmedi).</summary>
        public DateTime? RefreshExpiresAt { get; set; }
        public bool RememberMe { get; set; }
        /// <summary>Güncel koşullar kabul edilmemiş → AcceptTerms ekranı gösterilmeli.</summary>
        public bool ConsentRequired { get; set; }
        /// <summary>Güncel koşul sürümü (legal.termsVersion).</summary>
        public string TermsVersion { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        /// <summary>Sosyal girişte yeni hesap açıldı.</summary>
        public bool IsNew { get; set; }
        public bool ProfileIncomplete { get; set; }
        public List<string> MissingProfileFields { get; set; } = new();
    }
}
