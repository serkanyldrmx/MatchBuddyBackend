namespace MatchBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        /// <summary>Kimlik doğrulama başarı metinleri ve e-posta/SMS şablonları (MAIL_* / SMS_*) — docs/auth-api.md.</summary>
        public static partial class Auth
        {
            public const string LoggedOut = "MSG_AUTH_LOGGED_OUT";
            public const string LoggedOutAll = "MSG_AUTH_LOGGED_OUT_ALL";
            public const string ResetRequested = "MSG_AUTH_RESET_REQUESTED";
            public const string PasswordReset = "MSG_AUTH_PASSWORD_RESET";
            public const string PasswordChanged = "MSG_AUTH_PASSWORD_CHANGED";
            public const string EmailVerified = "MSG_AUTH_EMAIL_VERIFIED";
            public const string VerificationSent = "MSG_AUTH_VERIFICATION_SENT";
            public const string TermsAccepted = "MSG_AUTH_TERMS_ACCEPTED";
            public const string ExternalUnlinked = "MSG_AUTH_EXTERNAL_UNLINKED";

            // Şablonlar. Yer tutucular resx yorumlarında.
            public const string MailGreeting = "MAIL_GREETING";                         // {0} ad
            public const string MailFooter = "MAIL_FOOTER";
            public const string MailIgnoreNotice = "MAIL_IGNORE_NOTICE";
            public const string MailResetSubject = "MAIL_RESET_SUBJECT";
            public const string MailResetBody = "MAIL_RESET_BODY";                      // {0} dakika
            public const string MailVerifySubject = "MAIL_VERIFY_SUBJECT";
            public const string MailVerifyBody = "MAIL_VERIFY_BODY";                    // {0} dakika
            public const string MailVerifyButton = "MAIL_VERIFY_BUTTON";
            public const string MailPasswordChangedSubject = "MAIL_PASSWORD_CHANGED_SUBJECT";
            public const string MailPasswordChangedBody = "MAIL_PASSWORD_CHANGED_BODY"; // {0} tarih/saat
            public const string SmsResetBody = "SMS_RESET_BODY";                        // {0} kod, {1} dakika

            /// <summary>Sosyal girişte sağlayıcı ad vermediyse yeni hesabın adı.</summary>
            public const string DefaultPlayerName = "LABEL_AUTH_DEFAULT_PLAYER_NAME";
        }
    }
}
