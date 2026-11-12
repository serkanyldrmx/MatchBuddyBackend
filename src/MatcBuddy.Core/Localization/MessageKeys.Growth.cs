namespace MatcBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        /// <summary>İşletme büyüme araçları (docs/venue-growth-api.md).</summary>
        public static partial class Growth
        {
            public const string DealTitle = "LABEL_DEAL_TITLE";                         // kampanya başlığı
            public const string LabelRepeatWeeks = "LABEL_REPEAT_WEEKS";
            public const string DealSent = "MSG_DEAL_SENT";                             // {0} kişi
            public const string NotifDeal = "NOTIF_SLOT_DEAL";                          // {0} tesis, {1} tarih, {2} saha, {3} indirim %
            public const string SeriesSaved = "MSG_SERIES_SAVED";                       // {0} eklenen hafta, {1} atlanan
            public const string SeriesRemoved = "MSG_SERIES_REMOVED";                   // {0} kayıt
            public const string RepeatStopped = "MSG_REPEAT_STOPPED";
            public const string NotifSeriesInvite = "NOTIF_SERIES_INVITE";              // {0} maç, {1} tarih
            public const string NotifSeriesCreated = "NOTIF_SERIES_CREATED";            // {0} maç, {1} tarih
            public const string NotifSeriesSkipped = "NOTIF_SERIES_SKIPPED";            // {0} maç, {1} tarih
            public const string NotifSeriesOwnerAuto = "NOTIF_SERIES_OWNER_AUTO";       // {0} maç, {1} tarih, {2} saha
            public const string NotifSeriesOwnerPending = "NOTIF_SERIES_OWNER_PENDING"; // {0} maç, {1} tarih, {2} saha
        }
    }
}
