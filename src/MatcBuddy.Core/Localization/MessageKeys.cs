namespace MatcBuddy.Core.Localization
{
    /// <summary>
    /// Hata olmayan kullanıcı metinlerinin anahtarları (başarı mesajları, bildirim metinleri, alan etiketleri).
    /// Kural: MSG_* başarı/bilgi, NOTIF_* bildirim metni, LABEL_* alan/etiket adı.
    /// Metinler Resources/Messages.resx'te. Alan sınıfları partial'dır (MessageKeys.*.cs).
    /// </summary>
    public static partial class MessageKeys
    {
        public static partial class Common
        {
            public const string Added = "MSG_COMMON_ADDED";
            public const string Updated = "MSG_COMMON_UPDATED";
            public const string Deleted = "MSG_COMMON_DELETED";
            public const string Listed = "MSG_COMMON_LISTED";
            public const string Saved = "MSG_COMMON_SAVED";
            public const string PlayersListed = "MSG_COMMON_PLAYERS_LISTED";
            public const string GroupsListed = "MSG_COMMON_GROUPS_LISTED";
        }

        public static partial class Parameter
        {
            public const string Updated = "MSG_PARAM_UPDATED";
        }
    }
}
