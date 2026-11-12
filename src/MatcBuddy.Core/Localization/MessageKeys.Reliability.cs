namespace MatcBuddy.Core.Localization
{
    public static partial class MessageKeys
    {
        /// <summary>Güvenilirlik puanı metinleri — docs/reliability-api.md.</summary>
        public static class Reliability
        {
            public const string LeftWithPenalty = "MSG_RELIABILITY_LEFT_WITH_PENALTY";       // {0} düşen puan, {1} yeni puan
            public const string NoShowsSaved = "MSG_RELIABILITY_NOSHOWS_SAVED";              // {0} kişi
            public const string ExcuseAccepted = "MSG_RELIABILITY_EXCUSE_ACCEPTED";          // {0} oyuncu
            public const string NotifWithdrew = "NOTIF_RELIABILITY_WITHDREW";                // {0} oyuncu, {1} maç, {2} mazeret
            public const string NotifWithdrewNoReason = "NOTIF_RELIABILITY_WITHDREW_NO_REASON"; // {0} oyuncu, {1} maç
            public const string NotifExcuseAccepted = "NOTIF_RELIABILITY_EXCUSE_ACCEPTED";   // {0} maç, {1} iade edilen puan
            public const string NotifNoShow = "NOTIF_RELIABILITY_NOSHOW";                    // {0} maç, {1} düşen puan
            public const string ReasonPrefix = "LABEL_EXCUSE_";                              // + EMERGENCY / HEALTH / …
        }
    }
}
