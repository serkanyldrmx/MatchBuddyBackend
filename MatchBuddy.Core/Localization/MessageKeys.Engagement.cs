namespace MatchBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        public static partial class Message
        {
            public const string LabelDeleted = "LABEL_MESSAGE_DELETED";
            public const string Deleted = "MSG_MESSAGE_DELETED";
        }

        public static partial class Share
        {
            public const string Text = "SHARE_MATCH_TEXT";                       // {0} maç, {1} tarih, {2} saha, {3} eksik, {4} link
            public const string TextFull = "SHARE_MATCH_TEXT_FULL";              // {0} maç, {1} tarih, {2} saha, {3} link
            public const string OgTitle = "SHARE_OG_TITLE";                      // {0} maç, {1} eksik
            public const string OgTitleFull = "SHARE_OG_TITLE_FULL";             // {0} maç
            public const string OgDescription = "SHARE_OG_DESCRIPTION";          // {0} tarih, {1} saha
            public const string OgPerPlayer = "SHARE_OG_PER_PLAYER";             // {0} tutar
            public const string NotFoundTitle = "SHARE_NOT_FOUND_TITLE";
        }

        public static partial class External
        {
            public const string Linked = "MSG_EXTERNAL_LINKED";                  // {0} saha, {1} maç
        }
    }
}
