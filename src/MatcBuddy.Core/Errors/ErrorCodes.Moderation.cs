namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        public static partial class Auth
        {
            // Yönetici engeli: giriş ve oturum yenileme reddedilir (403 girişte, 401 yenilemede)
            public const string AccountBanned = "AUTH_ACCOUNT_BANNED";                 // süresiz
            public const string AccountBannedUntil = "AUTH_ACCOUNT_BANNED_UNTIL";      // {0} bitiş tarihi
        }

        /// <summary>Admin → Kullanıcılar / Şikayetler.</summary>
        public static partial class Moderation
        {
            public const string CannotBanSelf = "MOD_CANNOT_BAN_SELF";
            public const string CannotBanAdmin = "MOD_CANNOT_BAN_ADMIN";
            public const string BanUntilPast = "MOD_BAN_UNTIL_PAST";
        }

        /// <summary>İletişim formu mesajları.</summary>
        public static partial class Contact
        {
            public const string NotFound = "CONTACT_NOT_FOUND";
        }
    }
}
